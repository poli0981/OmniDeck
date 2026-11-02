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

    private void Start(TimeSpan from, bool play)
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
            PlayerChanged?.Invoke(this, EventArgs.Empty);
        }

        _player.Volume = _volume;
        _player.Muted = _muted;
        _position = from;
        _player.Open(path, from, play);
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
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTimeChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void Save() => _store.Save(Queue, _position);
}
