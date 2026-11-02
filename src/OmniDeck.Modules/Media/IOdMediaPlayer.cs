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

    /// <summary>The file's audio tracks, libVLC's "Disable" among them.</summary>
    IReadOnlyList<OdMediaTrack> AudioTracks { get; }

    /// <summary>The file's subtitle tracks, and the ones added from files, libVLC's "Disable" among them.</summary>
    IReadOnlyList<OdMediaTrack> SubtitleTracks { get; }

    /// <summary>The audio track in use, by id.</summary>
    int AudioTrack { get; set; }

    /// <summary>The subtitle track shown, by id; -1 shows none.</summary>
    int SubtitleTrack { get; set; }

    /// <summary>The playback speed; 1 is normal.</summary>
    float Rate { get; set; }

    IReadOnlyList<OdMediaDevice> AudioDevices { get; }

    /// <summary>The audio output device, by libVLC's id; null leaves Windows' default.</summary>
    string? AudioDevice { get; set; }

    /// <summary>From 0 to 100.</summary>
    int Volume { get; set; }

    bool Muted { get; set; }

    /// <summary>Opens a file, as <paramref name="opening"/> says.</summary>
    void Open(string path, OdMediaOpening opening);

    /// <summary>Shows a subtitle file with the file playing; false when libVLC cannot take it.</summary>
    bool AddSubtitle(string path);

    /// <summary>Saves the picture on screen as a PNG; false when there is none.</summary>
    bool TakeSnapshot(string path);

    void Play();

    void Pause();

    void Stop();

    void Seek(TimeSpan time);
}
