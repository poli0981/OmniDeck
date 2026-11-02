using LibVLCSharp.Shared;
using Serilog;
using Serilog.Events;

namespace OmniDeck.Modules.Media;

/// <summary>
/// libVLC for the media module, started with the first player it makes; disposing it lets libVLC go
/// (docs/spikes/06-libvlcsharp.md: playback holds 280–520 MB). Its errors go to the app log as
/// warnings, less the known noise. Used on the UI thread.
/// </summary>
internal sealed class OdVlcEngine(ILogger log) : IOdMediaEngine
{
    private readonly ILogger _log = log.ForContext<OdVlcEngine>();
    private LibVLC? _libVlc;

    public IOdMediaPlayer CreatePlayer()
    {
        var ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Players are made on the UI thread.");
        _libVlc ??= Start();
        return new OdVlcPlayer(_libVlc, ui);
    }

    public void Dispose()
    {
        if (_libVlc is { } libVlc)
        {
            _libVlc = null;
            libVlc.Log -= OnLog;
            libVlc.Dispose();
            _log.Information("libVLC let go");
        }
    }

    private LibVLC Start()
    {
        // Loads libvlc.dll from libvlc\win-x64 next to the app; a second call does nothing.
        LibVLCSharp.Shared.Core.Initialize();
        var libVlc = new LibVLC("--no-video-title-show");
        libVlc.Log += OnLog;
        _log.Information("libVLC {Version} ready", libVlc.Version);
        return libVlc;
    }

    private void OnLog(object? sender, LogEventArgs e)
    {
        if (e.Level == LogLevel.Error && OdVlcLog.Keep(e.Message))
        {
            _log.Write(LogEventLevel.Warning, "libVLC {Module}: {Message}", e.Module, e.Message);
        }
    }
}
