using System.Globalization;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace OmniDeck.Modules.Media;

/// <summary>
/// libVLC's player. Its events arrive on libVLC's threads, where calling back into the player can
/// deadlock, so they are posted to the UI thread first.
/// </summary>
internal sealed class OdVlcPlayer : IOdMediaPlayer
{
    private readonly LibVLC _libVlc;
    private readonly SynchronizationContext _ui;
    private VlcMedia? _media;

    public OdVlcPlayer(LibVLC libVlc, SynchronizationContext ui)
    {
        _libVlc = libVlc;
        _ui = ui;
        Native = new MediaPlayer(libVlc) { EnableHardwareDecoding = true };
        Native.Opening += (_, _) => Post(OdMediaState.Opening);
        Native.Playing += (_, _) => Post(OdMediaState.Playing);
        Native.Paused += (_, _) => Post(OdMediaState.Paused);
        Native.Stopped += (_, _) => Post(OdMediaState.Stopped);
        Native.EndReached += (_, _) => Post(OdMediaState.Ended);
        Native.EncounteredError += (_, _) => Post(OdMediaState.Failed);
        Native.TimeChanged += (_, _) => _ui.Post(_ => TimeChanged?.Invoke(this, EventArgs.Empty), null);
        Native.LengthChanged += (_, _) => _ui.Post(_ => TimeChanged?.Invoke(this, EventArgs.Empty), null);
    }

    public event EventHandler? StateChanged;

    public event EventHandler? TimeChanged;

    /// <summary>The player the view's <c>VideoView</c> shows.</summary>
    public MediaPlayer Native { get; }

    public OdMediaState State { get; private set; }

    public TimeSpan Time => TimeSpan.FromMilliseconds(Math.Max(0, Native.Time));

    public TimeSpan Length => TimeSpan.FromMilliseconds(Math.Max(0, Native.Length));

    public bool HasVideo => Native.VideoTrackCount > 0;

    public int Volume
    {
        get => Native.Volume;
        set => Native.Volume = Math.Clamp(value, 0, 100);
    }

    public bool Muted
    {
        get => Native.Mute;
        set => Native.Mute = value;
    }

    public void Open(string path, TimeSpan from, bool play)
    {
        var media = new VlcMedia(_libVlc, path, FromType.FromPath);
        if (from > TimeSpan.Zero)
        {
            media.AddOption(":start-time=" + from.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (!play)
        {
            media.AddOption(":start-paused");
        }

        _media?.Dispose();
        _media = media;
        State = OdMediaState.Opening;
        Native.Play(media);
    }

    public void Play() => Native.SetPause(false);

    public void Pause() => Native.SetPause(true);

    public void Stop() => Native.Stop();

    public void Seek(TimeSpan time) => Native.Time = (long)time.TotalMilliseconds;

    public void Dispose()
    {
        Native.Stop();
        Native.Dispose();
        _media?.Dispose();
        _media = null;
    }

    private void Post(OdMediaState state) => _ui.Post(
        _ =>
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        },
        null);
}
