using System.Net.Http;
using System.Windows;
using OmniDeck.Core.Storage;
using Serilog;
using Serilog.Core;

namespace OmniDeck.Core.Modules;

/// <summary>
/// Runs the modules (docs/ARCHITECTURE.md §2): initializes each one on first use, keeps the views of
/// the visible ones (the main window's module and those in windows of their own), and moves the
/// others down the state ladder when the shell or the RAM governor asks. Calls come from the UI thread
/// and run one at a time, so a slow module never interleaves two transitions.
/// </summary>
public sealed class OdModuleHost : IDisposable
{
    private readonly Dictionary<string, OdHostedModule> _byId;
    private readonly OdDatabase _database;
    private readonly ILogger _baseLog;
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OdModuleHost(IEnumerable<IOdModule> modules, OdDatabase database, ILogger log, TimeProvider time, IOdNotifier notifier, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(log);
        _database = database;
        _baseLog = log;
        _log = log.ForContext<OdModuleHost>();
        _time = time;
        _notifier = notifier;
        _http = http;
        // A repeated id keeps the first module: two would share one settings document, and a catalog
        // entry named like a built-in module must not stop OmniDeck from starting.
        var hosted = new List<OdHostedModule>();
        _byId = new Dictionary<string, OdHostedModule>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            var entry = new OdHostedModule(module);
            if (_byId.TryAdd(entry.Info.Id, entry))
            {
                hosted.Add(entry);
            }
            else
            {
                _log.Warning("Module {ModuleId} is registered twice; the first one is kept", entry.Info.Id);
            }
        }

        Modules = hosted;
    }

    public event EventHandler<OdModuleStateChangedEventArgs>? StateChanged;

    /// <summary>Every registered module, in registration order.</summary>
    public IReadOnlyList<OdHostedModule> Modules { get; }

    /// <summary>The module visible in the main window; <see langword="null"/> while the shell shows one of its own pages.</summary>
    public OdHostedModule? Current { get; private set; }

    public OdHostedModule? Find(string moduleId) => _byId.GetValueOrDefault(moduleId);

    /// <summary>
    /// Makes <paramref name="moduleId"/> the visible module: initializes it on first use, builds its
    /// view when it has none (first open, or after hibernation) and hides the module shown before.
    /// The previous module stays visible until the new one is active, so a module that fails to start
    /// leaves the shell as it was. A module in a window of its own is docked first (<see cref="DockAsync"/>).
    /// </summary>
    public async Task<FrameworkElement> ShowAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (hosted.IsPoppedOut)
            {
                // Its view belongs to the other window until that window lets go of it.
                throw new InvalidOperationException($"Module '{moduleId}' shows in a window of its own.");
            }

            if (hosted == Current && hosted.View is { } shown)
            {
                return shown;
            }

            var view = await EnsureViewAsync(hosted);
            await hosted.Module.ActivateAsync();
            HideCurrent();
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

    /// <summary>
    /// Moves <paramref name="moduleId"/> into a window of its own (a pop-out). Like <see cref="ShowAsync"/>,
    /// it initializes the module and builds its view when it has none; the module stays active, out of
    /// the governor's reach, until <see cref="DockAsync"/>. The main window's module stays as it is,
    /// unless it is this one, which the main window then no longer shows.
    /// </summary>
    public async Task<FrameworkElement> PopOutAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            var view = await EnsureViewAsync(hosted);
            if (hosted.State != OdModuleState.Active)
            {
                await hosted.Module.ActivateAsync();
            }

            if (hosted == Current)
            {
                Current = null;
            }

            hosted.IsPoppedOut = true;
            hosted.LastActive = _time.GetUtcNow();
            SetState(hosted, OdModuleState.Active);
            return view;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Its window closed: the module goes back to the main window, hidden, and down the ladder like any hidden module.</summary>
    public async Task DockAsync(string moduleId)
    {
        var hosted = Get(moduleId);
        await _gate.WaitAsync();
        try
        {
            if (!hosted.IsPoppedOut)
            {
                return;
            }

            hosted.IsPoppedOut = false;
            hosted.LastActive = _time.GetUtcNow();
            SetState(hosted, OdModuleState.Idle);
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
            }

            if (hosted.State == OdModuleState.Active)
            {
                hosted.LastActive = _time.GetUtcNow();
            }

            hosted.IsPoppedOut = false;
            SetState(hosted, OdModuleState.Closed);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Closes every open module before OmniDeck exits, so each one can save its state and let go of
    /// what it holds; a web app's browser process, for one, writes its cookies out only when it closes
    /// cleanly. A module that fails to close is logged and the rest still close.
    /// </summary>
    public async Task CloseAllAsync()
    {
        var open = Modules.Where(hosted => hosted.State is not (OdModuleState.Created or OdModuleState.Closed)).Select(hosted => hosted.Info.Id).ToList();
        foreach (var moduleId in open)
        {
            try
            {
                await CloseAsync(moduleId);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Module {ModuleId} failed to close", moduleId);
            }
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

    private async Task<FrameworkElement> EnsureViewAsync(OdHostedModule hosted)
    {
        if (!hosted.IsInitialized)
        {
            await hosted.Module.InitializeAsync(CreateContext(hosted.Info.Id));
            hosted.IsInitialized = true;
        }

        var view = hosted.View ?? hosted.Module.CreateView();
        hosted.View = view;
        return view;
    }

    private OdModuleContext CreateContext(string moduleId) => new(
        moduleId,
        new OdModuleSettingsStore(_database, moduleId),
        _baseLog.ForContext(Constants.SourceContextPropertyName, "Module." + moduleId),
        _time,
        new OdModuleNotifier(_notifier, moduleId),
        _http,
        _database);

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
