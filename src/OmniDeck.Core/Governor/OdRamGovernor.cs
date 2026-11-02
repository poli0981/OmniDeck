using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using Serilog;

namespace OmniDeck.Core.Governor;

/// <summary>
/// The RAM governor (docs/RAM-GOVERNOR.md). Every poll it moves hidden modules down the ladder by
/// how long they have been hidden, then enforces the caps on the private commit of OmniDeck's own
/// process and of the processes the memory reporters follow, the web runner's: above the soft cap it
/// hibernates the least recently used module, above the hard cap as many as it takes, and says so
/// once. Modules the user keeps running, and modules that refuse to suspend, are never demoted;
/// modules that work while hidden (<see cref="IOdResidency"/>) give way only to the caps. It works
/// through <see cref="OdModuleHost"/> on the UI thread; the task-manager panel uses the same methods.
/// </summary>
public sealed class OdRamGovernor : IDisposable
{
    private readonly OdModuleHost _host;
    private readonly IOdSettingsService _settings;
    private readonly IOdMemory _memory;
    private readonly IReadOnlyList<IOdMemoryReporter> _reporters;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private readonly ILogger _log;
    private ITimer? _timer;
    private bool _warnedAboveHardCap;
    private int _ticking;

    public OdRamGovernor(
        OdModuleHost host,
        IOdSettingsService settings,
        IOdMemory memory,
        IEnumerable<IOdMemoryReporter> reporters,
        TimeProvider time,
        IOdNotifier notifier,
        ILogger log)
    {
        ArgumentNullException.ThrowIfNull(reporters);
        ArgumentNullException.ThrowIfNull(log);
        _host = host;
        _settings = settings;
        _memory = memory;
        _reporters = [.. reporters];
        _time = time;
        _notifier = notifier;
        _log = log.ForContext<OdRamGovernor>();
    }

    /// <summary>Raised after every pass, with the memory in use and the limits it was held to.</summary>
    public event EventHandler<OdGovernorSampledEventArgs>? Sampled;

    /// <summary>The thresholds in effect, from the settings and this machine's memory.</summary>
    public OdGovernorOptions Options => OdGovernorOptions.From(_settings.Current.Memory, _memory.PhysicalBytes);

    /// <summary>What the last pass measured, less what it hibernated; null before the first pass.</summary>
    public OdMemorySnapshot? Snapshot { get; private set; }

    /// <summary>OmniDeck's private commit at the last pass, or its own process's now if no pass ran yet.</summary>
    public long CommittedBytes => Snapshot?.TotalBytes ?? _memory.CommittedBytes();

    public bool IsKeptRunning(string moduleId) => _settings.Current.Memory.KeepRunning.Contains(moduleId, StringComparer.Ordinal);

    /// <summary>Keeps a module running, or lets the governor demote it again.</summary>
    public void SetKeptRunning(string moduleId, bool keep)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        if (keep == IsKeptRunning(moduleId))
        {
            return;
        }

        _settings.Update(s => s with
        {
            Memory = s.Memory with
            {
                KeepRunning = keep
                    ? [.. s.Memory.KeepRunning, moduleId]
                    : [.. s.Memory.KeepRunning.Where(id => !string.Equals(id, moduleId, StringComparison.Ordinal))],
            },
        });
    }

    /// <summary>Polls every <see cref="OdGovernorOptions.PollInterval"/> on the calling thread, which must be the UI thread.</summary>
    public void Start()
    {
        var context = SynchronizationContext.Current
            ?? throw new InvalidOperationException("Start the governor from the UI thread: it drives module views.");
        var interval = Options.PollInterval;
        _timer?.Dispose();
        _timer = _time.CreateTimer(_ => context.Post(_ => Poll(), null), null, interval, interval);
    }

    /// <summary>One pass: the time ladder first, then the caps.</summary>
    public async Task TickAsync()
    {
        var options = Options;
        var now = _time.GetUtcNow();
        var demoted = false;
        foreach (var hosted in Demotable().Where(hosted => !StaysResident(hosted)))
        {
            var hiddenFor = now - hosted.LastActive;
            if (hiddenFor >= options.HibernateAfter)
            {
                demoted |= await _host.HibernateAsync(hosted.Info.Id);
            }
            else if (hiddenFor >= options.IdleAfter && hosted.State == OdModuleState.Idle)
            {
                demoted |= await _host.SuspendAsync(hosted.Info.Id, OdSuspendReason.Hidden);
            }
        }

        if (demoted)
        {
            _memory.Reclaim();
        }

        var snapshot = await MeasureAsync();
        if (snapshot.TotalBytes > options.HardCapBytes)
        {
            snapshot = await EnforceHardCapAsync(options, snapshot);
        }
        else
        {
            _warnedAboveHardCap = false;
            if (snapshot.TotalBytes > options.SoftCapBytes && await HibernateOneAsync(options, snapshot) is { } hibernated)
            {
                _memory.Reclaim();
                snapshot = snapshot.Without(hibernated, _memory.CommittedBytes());
            }
        }

        Snapshot = snapshot;
        Sampled?.Invoke(this, new OdGovernorSampledEventArgs(snapshot, options));
    }

    /// <summary>The panel's Suspend button.</summary>
    public Task<bool> SuspendAsync(string moduleId) => _host.SuspendAsync(moduleId, OdSuspendReason.User);

    /// <summary>The panel's Hibernate button.</summary>
    public async Task<bool> HibernateAsync(string moduleId)
    {
        var hibernated = await _host.HibernateAsync(moduleId);
        if (hibernated)
        {
            _memory.Reclaim();
        }

        return hibernated;
    }

    /// <summary>The panel's Close button.</summary>
    public async Task CloseAsync(string moduleId)
    {
        await _host.CloseAsync(moduleId);
        _memory.Reclaim();
    }

    public void Dispose() => _timer?.Dispose();

    private static bool StaysResident(OdHostedModule hosted) => hosted.Module is IOdResidency { StaysResident: true };

    private void Poll() => _ = PollAsync();

    private async Task PollAsync()
    {
        // A pass that is still awaiting a module skips the next tick instead of overlapping it.
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return;
        }

        try
        {
            await TickAsync();
        }
        catch (Exception ex)
        {
            // One misbehaving module must not stop the governor for the rest of the session.
            _log.Error(ex, "Governor pass failed");
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    /// <summary>OmniDeck's own process, plus what every reporter follows; a reporter that fails is left out of this pass.</summary>
    private async Task<OdMemorySnapshot> MeasureAsync()
    {
        var shared = 0L;
        var modules = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var reporter in _reporters)
        {
            OdMemoryReport report;
            try
            {
                report = await reporter.ReportAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log.Warning(ex, "A memory reporter failed; its processes are left out of this pass");
                continue;
            }

            shared += report.SharedBytes;
            foreach (var (moduleId, bytes) in report.Modules)
            {
                modules[moduleId] = modules.GetValueOrDefault(moduleId) + bytes;
            }
        }

        return new OdMemorySnapshot(_memory.CommittedBytes(), shared, modules);
    }

    /// <summary>Hidden, live modules the user did not keep running: the only ones the governor may demote.</summary>
    private List<OdHostedModule> Demotable() =>
        [.. _host.Modules.Where(hosted => (hosted.State is OdModuleState.Idle or OdModuleState.Suspended) && !IsKeptRunning(hosted.Info.Id))];

    /// <summary>
    /// The order the caps take modules in: those that work while hidden last, then the least recently
    /// used first. Recency counts in poll intervals: among modules hidden within the same interval the
    /// heavy one goes first, and then the one that uses more.
    /// </summary>
    private List<OdHostedModule> Victims(OdGovernorOptions options, OdMemorySnapshot snapshot)
    {
        var now = _time.GetUtcNow();
        return
        [
            .. Demotable()
                .OrderBy(hosted => StaysResident(hosted) ? 1 : 0)
                .ThenByDescending(hosted => (now - hosted.LastActive).Ticks / options.PollInterval.Ticks)
                .ThenBy(hosted => hosted.Info.RamClass == OdRamClass.Heavy ? 0 : 1)
                .ThenByDescending(hosted => snapshot.For(hosted.Info.Id) ?? 0),
        ];
    }

    /// <summary>Hibernates the first victim that agrees; returns its id.</summary>
    private async Task<string?> HibernateOneAsync(OdGovernorOptions options, OdMemorySnapshot snapshot)
    {
        foreach (var hosted in Victims(options, snapshot))
        {
            var resident = StaysResident(hosted);
            if (await _host.HibernateAsync(hosted.Info.Id))
            {
                _log.Information("Above the soft cap: hibernated {ModuleId}", hosted.Info.Id);
                TellIfResident(hosted, resident);
                return hosted.Info.Id;
            }
        }

        return null;
    }

    private async Task<OdMemorySnapshot> EnforceHardCapAsync(OdGovernorOptions options, OdMemorySnapshot snapshot)
    {
        var limit = OdBytes.Format(options.HardCapBytes);
        var victims = Victims(options, snapshot);
        if (SharedHoldsTheCap(options, snapshot, victims))
        {
            _log.Warning("Above the hard cap, held there by {Shared} of shared WebView2 processes: nothing hibernated", snapshot.SharedBytes);
            Warn(OdLocalization.Format("Governor_HardCapShared", limit, OdBytes.Format(snapshot.SharedBytes)));
            return snapshot;
        }

        var hibernated = 0;
        foreach (var hosted in victims)
        {
            if (snapshot.TotalBytes <= options.HardCapBytes)
            {
                break;
            }

            var resident = StaysResident(hosted);
            if (await _host.HibernateAsync(hosted.Info.Id))
            {
                hibernated++;
                TellIfResident(hosted, resident);
                _memory.Reclaim();
                snapshot = snapshot.Without(hosted.Info.Id, _memory.CommittedBytes());
            }
        }

        _log.Warning("Above the hard cap: hibernated {Count} modules, {Committed} committed", hibernated, snapshot.TotalBytes);
        Warn(hibernated switch
        {
            0 => OdLocalization.Format("Governor_HardCapKept", limit),
            1 => OdLocalization.Format("Governor_HardCapHibernatedOne", limit),
            _ => OdLocalization.Format("Governor_HardCapHibernated", hibernated, limit),
        });
        return snapshot;
    }

    /// <summary>
    /// Whether OmniDeck stays above the hard cap even once every victim is hibernated, because the web
    /// apps that remain keep the shared WebView2 processes, whose GPU process grows with video: then
    /// hibernating would close apps for nothing.
    /// </summary>
    private static bool SharedHoldsTheCap(OdGovernorOptions options, OdMemorySnapshot snapshot, List<OdHostedModule> victims)
    {
        var staying = snapshot.Modules
            .Where(pair => !victims.Exists(hosted => string.Equals(hosted.Info.Id, pair.Key, StringComparison.Ordinal)))
            .ToList();
        return staying.Count > 0 && snapshot.ShellBytes + snapshot.SharedBytes + staying.Sum(pair => pair.Value) > options.HardCapBytes;
    }

    /// <summary>A module that worked while hidden stops doing so: its notifications, for one. Clicking reopens it.</summary>
    private void TellIfResident(OdHostedModule hosted, bool resident)
    {
        if (resident)
        {
            _notifier.Notify(new OdNotification(
                OdLocalization.Format("Governor_ResidentHibernatedTitle", hosted.Info.Title),
                OdLocalization.Get("Governor_ResidentHibernated"),
                hosted.Info.Id));
        }
    }

    /// <summary>One notification per excursion above the hard cap, not one per poll.</summary>
    private void Warn(string message)
    {
        if (!_warnedAboveHardCap)
        {
            _warnedAboveHardCap = true;
            _notifier.Notify(OdLocalization.Get("Governor_HardCapTitle"), message);
        }
    }
}
