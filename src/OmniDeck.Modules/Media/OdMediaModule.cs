using System.IO;
using System.Windows;
using OmniDeck.Core.Modules;
using Serilog;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Media;

/// <summary>
/// The media player (docs/SPEC.md §3.6): local audio and video through libVLC, with a queue that
/// survives restarts. libVLC starts with the first file played. While something plays the module is
/// pinned; suspending lets the player go, and hibernating lets libVLC go too, since playback holds
/// 280–520 MB (docs/spikes/06-libvlcsharp.md).
/// </summary>
public sealed class OdMediaModule : IOdModule, IOdMediaControls
{
    private readonly Func<ILogger, IOdMediaEngine> _engines;
    private IOdModuleContext? _context;
    private IOdMediaEngine? _engine;
    private OdMediaSession? _session;
    private OdMediaViewModel? _viewModel;

    public OdMediaModule()
        : this(log => new OdVlcEngine(log))
    {
    }

    internal OdMediaModule(Func<ILogger, IOdMediaEngine> engines)
    {
        _engines = engines;
    }

    public OdModuleInfo Info { get; } = new("media", "Media_Name", nameof(SymbolRegular.MoviesAndTv24), "Category_Media", OdRamClass.Heavy);

    internal OdMediaSession? Session => _session;

    /// <summary>
    /// The file loaded in a live player. Without a player there is nothing for the tray to control: starting
    /// one while the view is away could make libVLC open a video window of its own.
    /// </summary>
    public string? NowPlaying => _session is { Player: not null, Queue.CurrentItem: { } path } ? Path.GetFileNameWithoutExtension(path) : null;

    public bool IsPlaying => _session?.IsPlaying == true;

    public void PlayPause()
    {
        if (NowPlaying is not null)
        {
            _session!.Toggle();
        }
    }

    public void Next()
    {
        if (NowPlaying is not null)
        {
            _session!.Next();
        }
    }

    public void Previous()
    {
        if (NowPlaying is not null)
        {
            _session!.Previous();
        }
    }

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var context = _context ?? throw new InvalidOperationException("The host initializes a module before its first view.");
        _session ??= new OdMediaSession(new OdMediaStore(context.Database), () => (_engine ??= _engines(context.Log)).CreatePlayer(), context.Log);
        var settings = context.Settings.Get(OdModulesJsonContext.Default.OdMediaSettings) ?? new OdMediaSettings();
        _viewModel = new OdMediaViewModel(_session, settings, saved => context.Settings.Set(saved, OdModulesJsonContext.Default.OdMediaSettings), context.Time);
        return new OdMediaView { DataContext = _viewModel };
    }

    public Task ActivateAsync() => Task.CompletedTask;

    /// <summary>Pinned while something plays (docs/RAM-GOVERNOR.md §3).</summary>
    public Task<bool> CanSuspendAsync() => Task.FromResult(_session?.IsPlaying != true);

    /// <summary>Lets the player go; libVLC stays loaded, so playing again starts at once.</summary>
    public Task SuspendAsync(OdSuspendReason reason)
    {
        _session?.Release();
        return Task.CompletedTask;
    }

    public Task HibernateAsync()
    {
        _session?.Release();
        _viewModel?.Dispose();
        _viewModel = null;
        _engine?.Dispose();
        _engine = null;
        return Task.CompletedTask;
    }

    public async Task CloseAsync()
    {
        await HibernateAsync();
        _session?.Dispose();
        _session = null;
    }
}
