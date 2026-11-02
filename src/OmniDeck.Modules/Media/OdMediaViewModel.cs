using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Media;

/// <summary>The media player's screen: the queue, the transport controls and the time. Used from the UI thread.</summary>
public sealed partial class OdMediaViewModel : ObservableObject, IDisposable
{
    private readonly OdMediaSession _session;
    private readonly Action<OdMediaSettings> _saveSettings;
    private readonly TimeProvider _time;
    private OdMediaSettings _settings;
    private bool _syncing;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private double _length;

    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>Whether a picture shows; otherwise the screen covers libVLC's empty video window.</summary>
    [ObservableProperty]
    private bool _showsVideo;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private double _volume;

    [ObservableProperty]
    private bool _muted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsQueue))]
    private bool _queueVisible;

    /// <summary>Whether the window covers the screen for the player; the queue steps aside meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsQueue))]
    [NotifyPropertyChangedFor(nameof(IsWindowed))]
    private bool _isFullscreen;

    [ObservableProperty]
    private string _message = "";

    internal OdMediaViewModel(OdMediaSession session, OdMediaSettings settings, Action<OdMediaSettings> saveSettings, TimeProvider time)
    {
        _session = session;
        _settings = settings;
        _saveSettings = saveSettings;
        _time = time;
        _session.Volume = settings.Volume;
        _session.Muted = settings.Muted;
        _session.Rate = settings.Rate;
        _session.SubtitleEncoding = settings.SubtitleEncoding;
        _session.AudioDevice = settings.AudioDevice;
        _volume = settings.Volume;
        _muted = settings.Muted;
        _queueVisible = settings.QueueVisible;
        _session.Changed += OnSessionChanged;
        _session.PlayerChanged += OnPlayerChanged;
        _session.Failed += OnFailed;
        Refresh();
    }

    /// <summary>The player came or went: the view hands it to its <c>VideoView</c>.</summary>
    public event EventHandler? PlayerChanged;

    public ObservableCollection<OdMediaItem> Items { get; } = [];

    public bool ShowsQueue => QueueVisible && !IsFullscreen;

    public bool IsWindowed => !IsFullscreen;

    internal IOdMediaPlayer? Player => _session.Player;

    internal IReadOnlyList<OdMediaTrack> AudioTracks => _session.AudioTracks;

    internal int AudioTrack => _session.AudioTrack;

    internal IReadOnlyList<OdMediaTrack> SubtitleTracks => _session.SubtitleTracks;

    internal int SubtitleTrack => _session.SubtitleTrack;

    internal IReadOnlyList<OdMediaDevice> AudioDevices => _session.AudioDevices;

    internal string? AudioDevice => _session.AudioDevice;

    internal string SubtitleEncoding => _session.SubtitleEncoding;

    internal float Rate => _session.Rate;

    internal void SetAudioTrack(int id) => _session.AudioTrack = id;

    internal void SetSubtitleTrack(int id) => _session.SubtitleTrack = id;

    internal void SetSubtitleEncoding(string encoding)
    {
        _session.SubtitleEncoding = encoding;
        SaveSettings(_settings with { SubtitleEncoding = _session.SubtitleEncoding });
    }

    internal void SetRate(float rate)
    {
        _session.Rate = rate;
        SaveSettings(_settings with { Rate = _session.Rate });
    }

    internal void SetAudioDevice(string? id)
    {
        _session.AudioDevice = id;
        SaveSettings(_settings with { AudioDevice = _session.AudioDevice });
    }

    /// <summary>Shows a subtitle file with the file playing.</summary>
    internal void AddSubtitle(string path) =>
        Message = _session.AddSubtitle(path) ? "" : OdLocalization.Format("Media_SubtitleFailed", Path.GetFileName(path));

    /// <summary>Adds files, or a playlist's files, and plays the first one added.</summary>
    public void Add(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (OdMediaFiles.IsPlaylist(path))
            {
                files.AddRange(OdM3u.Parse(File.ReadAllText(path), Path.GetDirectoryName(path) ?? ""));
            }
            else if (Directory.Exists(path))
            {
                files.AddRange(OdMediaFiles.InFolder(path));
            }
            else if (OdMediaFiles.IsMedia(path))
            {
                files.Add(path);
            }
        }

        Message = files.Count == 0 ? OdLocalization.Get("Media_NothingToPlay") : "";
        _session.Add(files, play: true);
    }

    /// <summary>Writes the queue to <paramref name="path"/> as an .m3u playlist.</summary>
    public void SavePlaylist(string path) => File.WriteAllText(path, OdM3u.Write(_session.Queue.Items));

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _session.PlayerChanged -= OnPlayerChanged;
        _session.Failed -= OnFailed;
    }

    internal static string Clock(TimeSpan time) => time.TotalHours >= 1
        ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
        : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    [RelayCommand]
    private void PlayPause() => _session.Toggle();

    [RelayCommand]
    private void Stop() => _session.Stop();

    [RelayCommand]
    private void Next() => _session.Next();

    [RelayCommand]
    private void Previous() => _session.Previous();

    [RelayCommand]
    private void PlayItem(OdMediaItem? item)
    {
        if (item is not null)
        {
            _session.PlayAt(item.Index);
        }
    }

    [RelayCommand]
    private void RemoveItem(OdMediaItem? item)
    {
        if (item is not null)
        {
            _session.RemoveAt(item.Index);
        }
    }

    [RelayCommand]
    private void Clear() => _session.Clear();

    [RelayCommand]
    private void TakeSnapshot() => Message = _session.TakeSnapshot(OdMediaSnapshots.Folder, _time.GetLocalNow()) is { } path
        ? OdLocalization.Format("Media_SnapshotSaved", path)
        : OdLocalization.Get("Media_SnapshotFailed");

    [RelayCommand]
    private void ToggleQueue() => QueueVisible = !QueueVisible;

    [RelayCommand]
    private void ToggleMute() => Muted = !Muted;

    partial void OnPositionChanged(double value)
    {
        if (!_syncing)
        {
            _session.Seek(TimeSpan.FromSeconds(value));
        }
    }

    partial void OnVolumeChanged(double value)
    {
        _session.Volume = (int)Math.Round(value);
        SaveSettings(_settings with { Volume = _session.Volume });
    }

    partial void OnMutedChanged(bool value)
    {
        _session.Muted = value;
        SaveSettings(_settings with { Muted = value });
    }

    partial void OnQueueVisibleChanged(bool value) => SaveSettings(_settings with { QueueVisible = value });

    private void SaveSettings(OdMediaSettings settings)
    {
        if (settings != _settings)
        {
            _settings = settings;
            _saveSettings(settings);
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Refresh();

    private void OnPlayerChanged(object? sender, EventArgs e) => PlayerChanged?.Invoke(this, EventArgs.Empty);

    private void OnFailed(object? sender, OdMediaFailedEventArgs e) => Message = OdLocalization.Format("Media_Failed", e.File);

    private void Refresh()
    {
        var queue = _session.Queue;
        if (Items.Count != queue.Items.Count || Items.Any(item => item.IsCurrent != (item.Index == queue.Current)))
        {
            Items.Clear();
            for (var i = 0; i < queue.Items.Count; i++)
            {
                Items.Add(new OdMediaItem(i, queue.Items[i], i == queue.Current));
            }
        }

        HasItems = queue.Items.Count > 0;
        Title = queue.CurrentItem is { } path ? Path.GetFileNameWithoutExtension(path) : OdLocalization.Get("Media_Empty");
        IsPlaying = _session.IsPlaying;
        ShowsVideo = _session.Player is { HasVideo: true, State: not (OdMediaState.Stopped or OdMediaState.Ended or OdMediaState.Failed) };
        var time = _session.Time;
        var length = _session.Length;
        _syncing = true;
        try
        {
            Length = Math.Max(length.TotalSeconds, 0);
            Position = Math.Min(time.TotalSeconds, Length);
        }
        finally
        {
            _syncing = false;
        }

        TimeText = length > TimeSpan.Zero ? Clock(time) + " / " + Clock(length) : Clock(time);
    }
}
