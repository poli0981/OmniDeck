using System.Windows;
using OmniDeck.Core.Storage;
using Serilog;
using Serilog.Core;

namespace OmniDeck.Core.Modules;

/// <summary>
/// Runs the modules (docs/ARCHITECTURE.md §2): initializes each one on first use, keeps the view of
/// the visible one, and moves the others down the state ladder when the shell or the RAM governor
/// asks. Calls come from the UI thread and run one at a time, so a slow module never interleaves two
/// transitions.
/// </summary>
public sealed class OdModuleHost : IDisposable
{
    private readonly Dictionary<string, OdHostedModule> _byId;
    private readonly OdDatabase _database;
    private readonly ILogger _baseLog;
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OdModuleHost(IEnumerable<IOdModule> modules, OdDatabase database, ILogger log, TimeProvider time, IOdNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(log);
        _database = database;
        _baseLog = log;
        _log = log.ForContext<OdModuleHost>();
        _time = time;
        _notifier = notifier;
        Modules = [.. modules.Select(module => new OdHostedModule(module))];

        // Throws on a repeated id: two modules would share one settings document.
        _byId = Modules.ToDictionary(hosted => hosted.Info.Id, StringComparer.Ordinal);
    }

    public event EventHandler<OdModuleStateChangedEventArgs>? StateChanged;

    /// <summary>Every registered module, in registration order.</summary>
    public IReadOnlyList<OdHostedModule> Modules { get; }

    /// <summary>The visible module; <see langword="null"/> while the shell shows one of its own pages.</summary>
    public OdHostedModule? Current { get; private set; }

    public OdHostedModule? Find(string moduleId) => _byId.GetValueOrDefault(moduleId);

    /// <summary>
    /// Makes <paramref name="moduleId"/> the visible module: initializes it on first use, builds its
    /// view when it has none (first open, or after hibernation) and hides the module shown before.
    /// </summary>
    public async Task<FrameworkElement> ShowAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (hosted == Current && hosted.View is { } shown)
            {
                return shown;
            }

            if (!hosted.IsInitialized)
            {
                await hosted.Module.InitializeAsync(CreateContext(hosted.Info.Id));
                hosted.IsInitialized = true;
            }

            var view = hosted.View ?? hosted.Module.CreateView();
            hosted.View = view;
            HideCurrent();
            await hosted.Module.ActivateAsync();
            Current = hosted;
            hosted.LastActive = _time.GetUtcNow();
            SetState(hosted, OdModuleState.Active);
            return view;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Hides the visible module while the shell shows one of its own pages.</summary>
    public async Task HideAsync()
    {
        await _gate.WaitAsync();
        try
        {
            HideCurrent();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Suspends a hidden module; <see langword="false"/> when it is visible, already further down, or pinned.</summary>
    public async Task<bool> SuspendAsync(string moduleId, OdSuspendReason reason)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (hosted.State != OdModuleState.Idle || !await hosted.Module.CanSuspendAsync())
            {
                return false;
            }

            await hosted.Module.SuspendAsync(reason);
            SetState(hosted, OdModuleState.Suspended);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Hibernates a hidden module and lets go of its view; <see langword="false"/> when it is visible, not running, or pinned.</summary>
    public async Task<bool> HibernateAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (hosted.State is not (OdModuleState.Idle or OdModuleState.Suspended) || !await hosted.Module.CanSuspendAsync())
            {
                return false;
            }

            await hosted.Module.HibernateAsync();
            hosted.View = null;
            SetState(hosted, OdModuleState.Hibernated);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Closes a module at the user's request, visible or not; reopening it builds a new view.</summary>
    public async Task CloseAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (hosted.State is OdModuleState.Created or OdModuleState.Closed)
            {
                return;
            }

            await hosted.Module.CloseAsync();
            hosted.View = null;
            if (hosted == Current)
            {
                Current = null;
                hosted.LastActive = _time.GetUtcNow();
            }

            SetState(hosted, OdModuleState.Closed);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private OdHostedModule Get(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        return _byId.TryGetValue(moduleId, out var hosted)
            ? hosted
            : throw new ArgumentException($"No module has the id '{moduleId}'.", nameof(moduleId));
    }

    private OdModuleContext CreateContext(string moduleId) => new(
        moduleId,
        new OdModuleSettingsStore(_database, moduleId),
        _baseLog.ForContext(Constants.SourceContextPropertyName, "Module." + moduleId),
        _time,
        _notifier);

    private void HideCurrent()
    {
        if (Current is not { } previous)
        {
            return;
        }

        Current = null;
        previous.LastActive = _time.GetUtcNow();
        SetState(previous, OdModuleState.Idle);
    }

    private void SetState(OdHostedModule hosted, OdModuleState state)
    {
        var previous = hosted.State;
        hosted.State = state;
        _log.Debug("Module {ModuleId}: {Previous} -> {State}", hosted.Info.Id, previous, state);
        StateChanged?.Invoke(this, new OdModuleStateChangedEventArgs(hosted, previous));
    }
}
