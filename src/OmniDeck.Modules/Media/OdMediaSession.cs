using System.IO;
using Serilog;

namespace OmniDeck.Modules.Media;

/// <summary>
/// What the media module keeps between views (docs/SPEC.md §3.6): the queue, where playback stands,
/// and the player while there is one. The player starts with the first file played; <see cref="Release"/>
/// lets it go and keeps the position, so the module can step down while paused. Used on the UI thread.
/// </summary>
internal sealed class OdMediaSession : IDisposable
{
    private readonly OdMediaStore _store;
    private readonly Func<IOdMediaPlayer> _newPlayer;
    private readonly ILogger _log;
    private IOdMediaPlayer? _player;
    private TimeSpan _position;
    private int _volume = 80;
    private bool _muted;
    private float _rate = OdMediaSpeeds.Normal;
    private string _subtitleEncoding = OdSubtitleEncodings.Automatic;
    private string? _audioDevice;
    private string? _subtitle;

    public OdMediaSession(OdMediaStore store, Func<IOdMediaPlayer> newPlayer, ILogger log)
    {
        _store = store;
        _newPlayer = newPlayer;
        _log = log;
        (Queue, _position) = store.Load();
    }

    /// <summary>The queue, the player or where playback stands changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The player came or went: the view shows the new one, or none.</summary>
    public event EventHandler? PlayerChanged;

    /// <summary>A file could not be played.</summary>
    public event EventHandler<OdMediaFailedEventArgs>? Failed;

    public OdMediaQueue Queue { get; }

    public IOdMediaPlayer? Player => _player;

    public OdMediaState State => _player?.State ?? OdMediaState.Idle;

    /// <summary>Whether something is playing: the module is pinned meanwhile (docs/RAM-GOVERNOR.md §3).</summary>
    public bool IsPlaying => State is OdMediaState.Opening or OdMediaState.Playing;

    public TimeSpan Time => _player?.Time ?? _position;

    public TimeSpan Length => _player?.Length ?? TimeSpan.Zero;

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _player?.Volume = _volume;
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            _player?.Muted = value;
        }
    }

    /// <summary>The playback speed, from a quarter to four times; it stays for the files that follow.</summary>
    public float Rate
    {
        get => _rate;
        set
        {
            _rate = OdMediaSpeeds.Clamp(value);
            _player?.Rate = _rate;
        }
    }

    /// <summary>The subtitles' text encoding; changing it opens the file again where it stood, as libVLC reads it only then.</summary>
    public string SubtitleEncoding
    {
        get => _subtitleEncoding;
        set
        {
            var encoding = OdSubtitleEncodings.Known(value);
            if (string.Equals(encoding, _subtitleEncoding, StringComparison.Ordinal))
            {
                return;
            }

            _subtitleEncoding = encoding;
            if (_player is { State: OdMediaState.Playing or OdMediaState.Paused } player)
            {
                Start(player.Time, play: player.State == OdMediaState.Playing, keepSubtitle: true);
            }
        }
    }

    /// <summary>The audio output device, by libVLC's id; null is Windows' default.</summary>
    public string? AudioDevice
    {
        get => _audioDevice;
        set
        {
            _audioDevice = string.IsNullOrEmpty(value) ? null : value;
            _player?.AudioDevice = _audioDevice;
        }
    }

    public IReadOnlyList<OdMediaTrack> AudioTracks => _player?.AudioTracks ?? [];

    public IReadOnlyList<OdMediaTrack> SubtitleTracks => _player?.SubtitleTracks ?? [];

    public IReadOnlyList<OdMediaDevice> AudioDevices => _player?.AudioDevices ?? [];

    public int AudioTrack
    {
        get => _player?.AudioTrack ?? -1;
        set => _player?.AudioTrack = value;
    }

    public int SubtitleTrack
    {
        get => _player?.SubtitleTrack ?? -1;
        set => _player?.SubtitleTrack = value;
    }

    public bool HasVideo => _player?.HasVideo == true;

    /// <summary>Shows a subtitle file with the file playing, and again if it reopens; false with nothing playing.</summary>
    public bool AddSubtitle(string path)
    {
        if (_player is not { } player || !player.AddSubtitle(path))
        {
            return false;
        }

        _subtitle = path;
        return true;
    }

    /// <summary>Saves the picture on screen into <paramref name="folder"/>; returns the file, or null without a picture.</summary>
    public string? TakeSnapshot(string folder, DateTimeOffset now)
    {
        if (_player is not { HasVideo: true } player || Queue.CurrentItem is not { } current)
        {
            return null;
        }

        Directory.CreateDirectory(folder);
        var path = OdMediaSnapshots.PathFor(folder, Path.GetFileNameWithoutExtension(current), now);
        return player.TakeSnapshot(path) ? path : null;
    }

    /// <summary>Adds files to the queue and saves it; with <paramref name="play"/>, plays the first of them.</summary>
    public void Add(IReadOnlyCollection<string> paths, bool play)
    {
        if (paths.Count == 0)
        {
            return;
        }

        var first = Queue.Add(paths);
        Save();
        if (play)
        {
            PlayAt(first);
        }
        else
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void PlayAt(int index)
    {
        if (Queue.MoveTo(index))
        {
            Start(TimeSpan.Zero, play: true);
        }
    }

    /// <summary>Plays or pauses; with no player yet, opens the current file where playback stood.</summary>
    public void Toggle()
    {
        switch (State)
        {
            case OdMediaState.Playing or OdMediaState.Opening:
                _player!.Pause();
                break;
            case OdMediaState.Paused:
                _player!.Play();
                break;
            default:
                if (Queue.CurrentItem is not null)
                {
                    Start(State == OdMediaState.Ended ? TimeSpan.Zero : _position, play: true);
                }

                break;
        }
    }

    public void Next()
    {
        if (Queue.Next())
        {
            Start(TimeSpan.Zero, play: true);
        }
    }

    public void Previous()
    {
        // Like a music player: past the first seconds, Previous starts the file over.
        if (Time > TimeSpan.FromSeconds(3) || !Queue.Previous())
        {
            Seek(TimeSpan.Zero);
            return;
        }

        Start(TimeSpan.Zero, play: true);
    }

    public void Seek(TimeSpan time)
    {
        _position = time;
        _player?.Seek(time);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _position = TimeSpan.Zero;
        _player?.Stop();
        Save();
    }

    public void RemoveAt(int index)
    {
        var wasCurrent = index == Queue.Current;
        Queue.RemoveAt(index);
        if (wasCurrent)
        {
            Release();
            _position = TimeSpan.Zero;
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Release();
        Queue.Clear();
        _position = TimeSpan.Zero;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Lets the player go and keeps where playback stood: suspending, hibernating, closing.</summary>
    public void Release()
    {
        if (_player is not { } player)
        {
            return;
        }

        _position = player.State is OdMediaState.Ended or OdMediaState.Stopped ? TimeSpan.Zero : player.Time;
        _player = null;
        player.StateChanged -= OnStateChanged;
        player.TimeChanged -= OnTimeChanged;
        player.Dispose();
        Save();
        PlayerChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Release();

    private void Start(TimeSpan from, bool play, bool keepSubtitle = false)
    {
        if (Queue.CurrentItem is not { } path)
        {
            return;
        }

        if (_player is null)
        {
            _player = _newPlayer();
            _player.StateChanged += OnStateChanged;
            _player.TimeChanged += OnTimeChanged;
            if (_audioDevice is not null)
            {
                _player.AudioDevice = _audioDevice;
            }

            PlayerChanged?.Invoke(this, EventArgs.Empty);
        }

        // A subtitle file belongs to the file it was added to.
        if (!keepSubtitle)
        {
            _subtitle = null;
        }

        _player.Volume = _volume;
        _player.Muted = _muted;
        _position = from;
        _player.Open(path, new OdMediaOpening(from, play, _subtitleEncoding, _subtitle));
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        switch (State)
        {
            case OdMediaState.Ended when Queue.HasNext:
                Next();
                return;
            case OdMediaState.Ended:
                _position = TimeSpan.Zero;
                Save();
                break;
            case OdMediaState.Failed:
                _log.Warning("A media file could not be played");
                Failed?.Invoke(this, new OdMediaFailedEventArgs(Path.GetFileName(Queue.CurrentItem ?? "")));
                break;
            case OdMediaState.Paused:
                _position = _player?.Time ?? _position;
                Save();
                break;
            case OdMediaState.Playing when _player is { } player && Math.Abs(player.Rate - _rate) > 0.001f:
                // libVLC starts each file at normal speed.
                player.Rate = _rate;
                break;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimeChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void Save() => _store.Save(Queue, _position);
}
