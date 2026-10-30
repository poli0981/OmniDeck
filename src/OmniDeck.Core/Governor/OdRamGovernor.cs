using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using Serilog;

namespace OmniDeck.Core.Governor;

/// <summary>
/// The RAM governor (docs/RAM-GOVERNOR.md). Every poll it moves hidden modules down the ladder by
/// how long they have been hidden, then enforces the caps on OmniDeck's private commit: above the
/// soft cap it hibernates the least recently used module, above the hard cap as many as it takes,
/// and says so once. Modules the user keeps running, and modules that refuse to suspend, are never
/// demoted. It works through <see cref="OdModuleHost"/> on the UI thread; the task-manager panel uses
/// the same methods.
/// </summary>
public sealed class OdRamGovernor : IDisposable
{
    private readonly OdModuleHost _host;
    private readonly IOdSettingsService _settings;
    private readonly IOdMemory _memory;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private readonly ILogger _log;
    private ITimer? _timer;
    private long? _sampled;
    private bool _warnedAboveHardCap;
    private int _ticking;

    public OdRamGovernor(OdModuleHost host, IOdSettingsService settings, IOdMemory memory, TimeProvider time, IOdNotifier notifier, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _host = host;
        _settings = settings;
        _memory = memory;
        _time = time;
        _notifier = notifier;
        _log = log.ForContext<OdRamGovernor>();
    }

    /// <summary>Raised after every pass, with the memory in use and the limits it was held to.</summary>
    public event EventHandler<OdGovernorSampledEventArgs>? Sampled;

    /// <summary>The thresholds in effect, from the settings and this machine's memory.</summary>
    public OdGovernorOptions Options => OdGovernorOptions.From(_settings.Current.Memory, _memory.PhysicalBytes);

    /// <summary>OmniDeck's private commit at the last pass, or now if no pass ran yet.</summary>
    public long CommittedBytes => _sampled ?? _memory.CommittedBytes();

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
        foreach (var hosted in Demotable())
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

        var bytes = _memory.CommittedBytes();
        if (bytes > options.HardCapBytes)
        {
            bytes = await EnforceHardCapAsync(options, bytes);
        }
        else
        {
            _warnedAboveHardCap = false;
            if (bytes > options.SoftCapBytes && await HibernateOneAsync(options))
            {
                _memory.Reclaim();
                bytes = _memory.CommittedBytes();
            }
        }

        _sampled = bytes;
        Sampled?.Invoke(this, new OdGovernorSampledEventArgs(bytes, options));
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

    /// <summary>Hidden, live modules the user did not keep running: the only ones the governor may demote.</summary>
    private List<OdHostedModule> Demotable() =>
        [.. _host.Modules.Where(hosted => (hosted.State is OdModuleState.Idle or OdModuleState.Suspended) && !IsKeptRunning(hosted.Info.Id))];

    /// <summary>
    /// Least recently used first. Recency counts in poll intervals: modules hidden within the same
    /// interval are equally recent, and then the heavy one goes first.
    /// </summary>
    private IEnumerable<OdHostedModule> Victims(OdGovernorOptions options)
    {
        var now = _time.GetUtcNow();
        return Demotable()
            .OrderByDescending(hosted => (now - hosted.LastActive).Ticks / options.PollInterval.Ticks)
            .ThenBy(hosted => hosted.Info.RamClass == OdRamClass.Heavy ? 0 : 1);
    }

    private async Task<bool> HibernateOneAsync(OdGovernorOptions options)
    {
        foreach (var id in Victims(options).Select(hosted => hosted.Info.Id))
        {
            if (await _host.HibernateAsync(id))
            {
                _log.Information("Above the soft cap: hibernated {ModuleId}", id);
                return true;
            }
        }

        return false;
    }

    private async Task<long> EnforceHardCapAsync(OdGovernorOptions options, long bytes)
    {
        var hibernated = 0;
        foreach (var id in Victims(options).Select(hosted => hosted.Info.Id))
        {
            if (bytes <= options.HardCapBytes)
            {
                break;
            }

            if (await _host.HibernateAsync(id))
            {
                hibernated++;
                _memory.Reclaim();
                bytes = _memory.CommittedBytes();
            }
        }

        _log.Warning("Above the hard cap: hibernated {Count} modules, {Committed} committed", hibernated, bytes);

        // One notification per excursion above the cap, not one per poll.
        if (!_warnedAboveHardCap)
        {
            _warnedAboveHardCap = true;
            var limit = OdBytes.Format(options.HardCapBytes);
            var message = hibernated switch
            {
                0 => OdLocalization.Format("Governor_HardCapKept", limit),
                1 => OdLocalization.Format("Governor_HardCapHibernatedOne", limit),
                _ => OdLocalization.Format("Governor_HardCapHibernated", hibernated, limit),
            };
            _notifier.Notify(OdLocalization.Get("Governor_HardCapTitle"), message);
        }

        return bytes;
    }
}
