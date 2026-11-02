namespace OmniDeck.Modules.Media;

/// <summary>
/// One player as the media module uses it: libVLC's in the app (<see cref="OdVlcPlayer"/>), a fake in
/// tests. Its events arrive on the thread that created it, the UI thread.
/// </summary>
internal interface IOdMediaPlayer : IDisposable
{
    /// <summary>The player started, paused, stopped, reached the end, or failed.</summary>
    event EventHandler? StateChanged;

    /// <summary>Playback moved on, or the length became known; a few times a second while playing.</summary>
    event EventHandler? TimeChanged;

    OdMediaState State { get; }

    TimeSpan Time { get; }

    TimeSpan Length { get; }

    /// <summary>Whether the file has a picture; an audio file has none.</summary>
    bool HasVideo { get; }

    /// <summary>From 0 to 100.</summary>
    int Volume { get; set; }

    bool Muted { get; set; }

    /// <summary>Opens a file and plays it from <paramref name="from"/>, or stands paused there when <paramref name="play"/> is false.</summary>
    void Open(string path, TimeSpan from, bool play);

    void Play();

    void Pause();

    void Stop();

    void Seek(TimeSpan time);
}
