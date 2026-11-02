using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Media;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.ModuleApps;

public sealed class OdMediaTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly OdDatabase _database;
    private readonly OdFakeMediaEngine _engine = new();

    public OdMediaTests()
    {
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Playlists_ReadPathsAndSkipCommentsAndUrls_AndWriteThemBack()
    {
        var folder = _temp.Combine("music");
        var text = "#EXTM3U\r\n#EXTINF:-1,One\r\none.mp3\r\n\r\nC:\\Music\\two.flac\r\nhttps://example.com/stream.mp3\r\n";

        var paths = OdM3u.Parse(text, folder);

        Assert.Equal(new[] { Path.Combine(folder, "one.mp3"), @"C:\Music\two.flac" }, paths, StringComparer.Ordinal);
        Assert.Equal("#EXTM3U\n#EXTINF:-1,one\n" + paths[0] + "\n#EXTINF:-1,two\nC:\\Music\\two.flac\n", OdM3u.Write(paths));
        Assert.Equal(paths, OdM3u.Parse(OdM3u.Write(paths), folder), StringComparer.Ordinal);
    }

    [Fact]
    public void Files_AreTakenByExtension_AndAFolderByName()
    {
        var folder = Directory.CreateDirectory(_temp.Combine("folder")).FullName;
        foreach (var name in new[] { "b.MP3", "a.mkv", "notes.txt", "c.m3u" })
        {
            File.WriteAllText(Path.Combine(folder, name), "");
        }

        Assert.True(OdMediaFiles.IsMedia("x.Flac"));
        Assert.False(OdMediaFiles.IsMedia("x.txt"));
        Assert.True(OdMediaFiles.IsPlaylist("list.M3U8"));
        Assert.Equal(new[] { "a.mkv", "b.MP3" }, OdMediaFiles.InFolder(folder).Select(Path.GetFileName), StringComparer.Ordinal);
        Assert.StartsWith("Media|*.mp3;*.flac;", OdMediaFiles.Filter("Media", "All"), StringComparison.Ordinal);
        Assert.EndsWith("|All|*.*", OdMediaFiles.Filter("Media", "All"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheQueue_KeepsItsPlace_AsFilesComeAndGo()
    {
        var queue = new OdMediaQueue([], 0);
        Assert.Equal(-1, queue.Current);

        Assert.Equal(0, queue.Add(["a", "b", "c"]));
        Assert.Equal("a", queue.CurrentItem);
        Assert.True(queue.Next());
        Assert.True(queue.HasNext);
        Assert.True(queue.HasPrevious);

        queue.RemoveAt(0);
        Assert.Equal(("b", 0), (queue.CurrentItem, queue.Current));
        queue.RemoveAt(0);
        Assert.Equal("c", queue.CurrentItem);
        Assert.False(queue.Next());
        queue.RemoveAt(0);
        Assert.Equal(-1, queue.Current);
        Assert.Null(queue.CurrentItem);

        Assert.Equal(1, new OdMediaQueue(["a", "b"], 7).Current);
    }

    [Fact]
    public void TheQueue_IsSaved_WithWherePlaybackStood()
    {
        var store = new OdMediaStore(_database);
        Assert.Empty(store.Load().Queue.Items);

        store.Save(new OdMediaQueue([@"C:\a.mp3", @"C:\b.mp3"], 1), TimeSpan.FromSeconds(42));
        var (queue, position) = store.Load();

        Assert.Equal(new[] { @"C:\a.mp3", @"C:\b.mp3" }, queue.Items, StringComparer.Ordinal);
        Assert.Equal(1, queue.Current);
        Assert.Equal(TimeSpan.FromSeconds(42), position);
    }

    [Fact]
    public void Playing_StartsAPlayer_MovesOnAtTheEnd_AndStopsAfterTheLast()
    {
        using var session = NewSession();
        var playerChanges = 0;
        session.PlayerChanged += (_, _) => playerChanges++;

        session.Add(["one.mp3", "two.mp3"], play: true);
        var player = Assert.Single(_engine.Players);
        Assert.True(session.IsPlaying);
        Assert.Equal(1, playerChanges);
        Assert.Equal(80, player.Volume);

        session.Toggle();
        Assert.Equal(OdMediaState.Paused, session.State);
        Assert.False(session.IsPlaying);
        session.Toggle();
        Assert.True(session.IsPlaying);

        player.Raise(OdMediaState.Ended);
        Assert.Equal(1, session.Queue.Current);
        Assert.Contains("open two.mp3 0 True", player.Calls, StringComparer.Ordinal);

        player.Raise(OdMediaState.Ended);
        Assert.False(session.IsPlaying);
        Assert.Equal(TimeSpan.Zero, new OdMediaStore(_database).Load().Position);
    }

    [Fact]
    public void Releasing_KeepsThePosition_AndTheNextPlayStartsThere()
    {
        using (var session = NewSession())
        {
            session.Add(["one.mp3"], play: true);
            var first = _engine.Players[0];
            first.Time = TimeSpan.FromSeconds(30);
            session.Toggle();

            session.Release();

            Assert.True(first.Disposed);
            Assert.Null(session.Player);
            Assert.Equal(TimeSpan.FromSeconds(30), session.Time);
        }

        using var restarted = NewSession();
        Assert.Equal(TimeSpan.FromSeconds(30), restarted.Time);
        restarted.Toggle();
        Assert.Contains("open one.mp3 30 True", _engine.Players[1].Calls, StringComparer.Ordinal);
    }

    [Fact]
    public void Previous_StartsTheFileOver_PastItsFirstSeconds()
    {
        using var session = NewSession();
        session.Add(["one.mp3", "two.mp3"], play: true);
        session.Next();
        var player = _engine.Players[0];
        player.Time = TimeSpan.FromSeconds(10);

        session.Previous();
        Assert.Equal(1, session.Queue.Current);
        Assert.Equal("seek 0", player.Calls[^1]);

        player.Time = TimeSpan.FromSeconds(1);
        session.Previous();
        Assert.Equal(0, session.Queue.Current);
    }

    [Fact]
    public void AFileThatFails_IsNamed_AndRemovingTheCurrentFileLetsThePlayerGo()
    {
        using var session = NewSession();
        string? failed = null;
        session.Failed += (_, e) => failed = e.File;
        session.Add([@"C:\Music\broken.mp3", @"C:\Music\fine.mp3"], play: true);

        _engine.Players[0].Raise(OdMediaState.Failed);
        Assert.Equal("broken.mp3", failed);

        session.RemoveAt(0);
        Assert.True(_engine.Players[0].Disposed);
        Assert.Equal(@"C:\Music\fine.mp3", session.Queue.CurrentItem);

        session.Clear();
        Assert.Empty(new OdMediaStore(_database).Load().Queue.Items);
    }

    [Fact]
    public void Options_ReachThePlayer_AndAChangedEncodingReopensWhereItStood()
    {
        using var session = NewSession();
        session.AudioDevice = "speakers";
        session.Rate = 9f;
        session.Add(["film.mkv", "next.mkv"], play: true);
        var player = _engine.Players[0];

        Assert.Equal("speakers", player.AudioDevice);
        Assert.Equal(4f, session.Rate);
        player.Rate = 1f;
        player.Raise(OdMediaState.Playing);
        Assert.Equal(4f, player.Rate);

        player.AudioTracks = [new(-1, "Disable"), new(1, "English")];
        player.SubtitleTracks = [new(-1, "Disable"), new(2, "Vietnamese")];
        session.AudioTrack = 1;
        session.SubtitleTrack = 2;
        Assert.Equal((1, 2), (player.AudioTrack, player.SubtitleTrack));
        Assert.Equal(2, session.AudioTracks.Count);
        Assert.Equal(2, session.SubtitleTracks.Count);

        Assert.True(session.AddSubtitle(@"C:\subs\film.vi.srt"));
        player.Time = TimeSpan.FromSeconds(42);
        session.SubtitleEncoding = "Windows-1258";
        Assert.Equal(new OdMediaOpening(TimeSpan.FromSeconds(42), true, "Windows-1258", @"C:\subs\film.vi.srt"), player.LastOpening);

        session.SubtitleEncoding = "nonsense";
        Assert.Equal("", player.LastOpening?.SubtitleEncoding);

        session.Next();
        Assert.Null(player.LastOpening?.Subtitle);
        Assert.Equal("", session.SubtitleEncoding);
    }

    [Fact]
    public void ASnapshot_NeedsAPicture_AndIsNamedAfterTheFile()
    {
        using var session = NewSession();
        var folder = _temp.Combine("shots");
        var now = new DateTimeOffset(2026, 11, 2, 9, 5, 7, TimeSpan.FromHours(7));

        Assert.Null(session.TakeSnapshot(folder, now));
        session.Add(["My: Film.mkv"], play: true);
        Assert.Null(session.TakeSnapshot(folder, now));

        _engine.Players[0].HasVideo = true;
        Assert.Equal(Path.Combine(folder, "My_ Film 2026-11-02 09-05-07.png"), session.TakeSnapshot(folder, now));
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Encodings_AndSpeeds_AreTheOnesTheMenuOffers()
    {
        Assert.All(OdSubtitleEncodings.All, encoding => Assert.False(string.IsNullOrEmpty(OdSubtitleEncodings.Title(encoding))));
        Assert.Equal(OdSubtitleEncodings.All.Count, OdSubtitleEncodings.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("Vietnamese (Windows-1258)", OdSubtitleEncodings.Title("Windows-1258"));
        Assert.Equal("Shift_JIS", OdSubtitleEncodings.Known("shift_jis"));
        Assert.Equal("", OdSubtitleEncodings.Known(null));
        Assert.Equal(0.25f, OdMediaSpeeds.Clamp(0.1f));
        Assert.Equal(1f, OdMediaSpeeds.Clamp(float.NaN));
        Assert.Equal(OdMediaSpeeds.All.Count, OdMediaSpeeds.All.Distinct().Count());
    }

    [Fact]
    public void TheScreen_KeepsTheSpeedEncodingAndDevice_InTheSettings() => OdSta.Run(() =>
    {
        using var session = NewSession();
        OdMediaSettings? saved = null;
        using var screen = new OdMediaViewModel(session, new OdMediaSettings { Rate = 1.5f, SubtitleEncoding = "Big5" }, settings => saved = settings, TimeProvider.System);
        Assert.Equal((1.5f, "Big5"), (session.Rate, session.SubtitleEncoding));

        screen.SetRate(2f);
        screen.SetSubtitleEncoding("UTF-8");
        screen.SetAudioDevice("headphones");

        Assert.Equal(2f, saved?.Rate);
        Assert.Equal("UTF-8", saved?.SubtitleEncoding);
        Assert.Equal("headphones", saved?.AudioDevice);

        screen.AddSubtitle(@"C:\subs\film.srt");
        Assert.Equal("film.srt could not be added as subtitles.", screen.Message);
        return Task.CompletedTask;
    });

    [Fact]
    public void TheTray_ControlsALoadedPlayer_AndNothingElse() => OdSta.Run(async () =>
    {
        var module = new OdMediaModule(_ => _engine);
        IOdMediaControls tray = module;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
        using var host = new OdModuleHost([module], _database, Logger.None, time, new OdFakeNotifier(), OdTestHttp.Client);

        Assert.Null(tray.NowPlaying);
        tray.PlayPause();
        Assert.Empty(_engine.Players);

        await host.ShowAsync("media");
        module.Session!.Add([@"C:\Music\01 one.mp3", @"C:\Music\02 two.mp3"], play: true);
        Assert.Equal("01 one", tray.NowPlaying);
        Assert.True(tray.IsPlaying);

        tray.PlayPause();
        Assert.False(tray.IsPlaying);
        tray.Next();
        Assert.Equal("02 two", tray.NowPlaying);
        tray.Previous();
        Assert.Equal("01 one", tray.NowPlaying);

        module.Session.Release();
        Assert.Null(tray.NowPlaying);
    });

    [Fact]
    public void InFullScreen_TheQueueStepsAside() => OdSta.Run(() =>
    {
        using var session = NewSession();
        using var screen = new OdMediaViewModel(session, new OdMediaSettings(), _ => { }, TimeProvider.System);
        Assert.True(screen.ShowsQueue);
        Assert.True(screen.IsWindowed);

        screen.IsFullscreen = true;
        Assert.False(screen.ShowsQueue);
        Assert.False(screen.IsWindowed);

        screen.IsFullscreen = false;
        screen.ToggleQueueCommand.Execute(null);
        Assert.False(screen.ShowsQueue);
        return Task.CompletedTask;
    });

    [Fact]
    public void LibVlcNoise_StaysOutOfTheLog()
    {
        Assert.False(OdVlcLog.Keep("main libvlc: option quiet does not exist"));
        Assert.False(OdVlcLog.Keep("direct3d11: SetThumbNailClip failed: 0x800706f4"));
        Assert.False(OdVlcLog.Keep("avcodec decoder: Not enough decoding slices"));
        Assert.True(OdVlcLog.Keep("filesystem: cannot open file"));
    }

    [Fact]
    public void TheScreen_ShowsTheQueueAndTheTime_AndKeepsTheVolume() => OdSta.Run(() =>
    {
        using var session = NewSession();
        OdMediaSettings? saved = null;
        var folder = Directory.CreateDirectory(_temp.Combine("album")).FullName;
        File.WriteAllText(Path.Combine(folder, "02 b.mp3"), "");
        File.WriteAllText(Path.Combine(folder, "01 a.mp3"), "");
        var playlist = _temp.Combine("list.m3u");
        File.WriteAllText(playlist, "c.flac\n");
        using var screen = new OdMediaViewModel(session, new OdMediaSettings { Volume = 30 }, settings => saved = settings, TimeProvider.System);

        Assert.Equal("Nothing to play yet", screen.Title);
        screen.Add([folder, playlist, _temp.Combine("notes.txt")]);

        Assert.Equal(new[] { "01 a", "02 b", "c" }, screen.Items.Select(item => item.Title), StringComparer.Ordinal);
        Assert.True(screen.Items[0].IsCurrent);
        Assert.Equal("01 a", screen.Title);
        Assert.True(screen.IsPlaying);
        Assert.Equal(30, _engine.Players[0].Volume);

        var player = _engine.Players[0];
        player.Length = TimeSpan.FromMinutes(3);
        player.Time = TimeSpan.FromSeconds(65);
        player.Raise(OdMediaState.Playing);
        Assert.Equal("1:05 / 3:00", screen.TimeText);
        Assert.False(screen.ShowsVideo);
        player.HasVideo = true;
        player.Raise(OdMediaState.Playing);
        Assert.True(screen.ShowsVideo);
        Assert.Equal(180, screen.Length);

        screen.Position = 90;
        Assert.Equal("seek 90", player.Calls[^1]);

        screen.Volume = 55;
        Assert.Equal(55, saved?.Volume);
        screen.ToggleQueueCommand.Execute(null);
        Assert.False(saved?.QueueVisible);

        screen.Add([_temp.Combine("notes.txt")]);
        Assert.Equal("None of those are files the player can open.", screen.Message);
        Assert.Equal("1:01:01", OdMediaViewModel.Clock(new TimeSpan(1, 1, 1)));
        return Task.CompletedTask;
    });

    [Fact]
    public void TheModule_IsPinnedWhilePlaying_AndLetsLibVlcGoWhenHibernated() => OdSta.Run(async () =>
    {
        var module = new OdMediaModule(_ => _engine);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
        using var host = new OdModuleHost([module, new OdFakeModule("clock")], _database, Logger.None, time, new OdFakeNotifier(), OdTestHttp.Client);
        await host.ShowAsync("media");
        module.Session!.Add(["one.mp3"], play: true);
        await host.ShowAsync("clock");

        Assert.False(await host.HibernateAsync("media"));
        Assert.False(_engine.Disposed);

        module.Session.Toggle();
        Assert.True(await host.SuspendAsync("media", OdSuspendReason.Hidden));
        Assert.True(_engine.Players[0].Disposed);
        Assert.False(_engine.Disposed);

        Assert.True(await host.HibernateAsync("media"));
        Assert.True(_engine.Disposed);
        Assert.Equal(OdRamClass.Heavy, module.Info.RamClass);
    });

    private OdMediaSession NewSession() => new(new OdMediaStore(_database), _engine.CreatePlayer, Logger.None);

    private sealed class OdFakeMediaEngine : IOdMediaEngine
    {
        public List<OdFakeMediaPlayer> Players { get; } = [];

        public bool Disposed { get; private set; }

        public IOdMediaPlayer CreatePlayer()
        {
            var player = new OdFakeMediaPlayer();
            Players.Add(player);
            return player;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class OdFakeMediaPlayer : IOdMediaPlayer
    {
        public event EventHandler? StateChanged;

        public event EventHandler? TimeChanged;

        public List<string> Calls { get; } = [];

        public OdMediaState State { get; private set; }

        public TimeSpan Time { get; set; }

        public TimeSpan Length { get; set; }

        public bool HasVideo { get; set; }

        public IReadOnlyList<OdMediaTrack> AudioTracks { get; set; } = [];

        public IReadOnlyList<OdMediaTrack> SubtitleTracks { get; set; } = [];

        public int AudioTrack { get; set; }

        public int SubtitleTrack { get; set; } = -1;

        public float Rate { get; set; } = 1f;

        public IReadOnlyList<OdMediaDevice> AudioDevices { get; set; } = [];

        public string? AudioDevice { get; set; }

        public OdMediaOpening? LastOpening { get; private set; }

        public int Volume { get; set; }

        public bool Muted { get; set; }

        public bool Disposed { get; private set; }

        public void Open(string path, OdMediaOpening opening)
        {
            LastOpening = opening;
            Calls.Add(string.Create(CultureInfo.InvariantCulture, $"open {Path.GetFileName(path)} {opening.From.TotalSeconds} {opening.Play}"));
            Time = opening.From;
            Raise(opening.Play ? OdMediaState.Playing : OdMediaState.Paused);
        }

        public bool AddSubtitle(string path) => !path.EndsWith("film.srt", StringComparison.Ordinal);

        public bool TakeSnapshot(string path) => HasVideo;

        public void Play()
        {
            Calls.Add("play");
            Raise(OdMediaState.Playing);
        }

        public void Pause()
        {
            Calls.Add("pause");
            Raise(OdMediaState.Paused);
        }

        public void Stop()
        {
            Calls.Add("stop");
            Raise(OdMediaState.Stopped);
        }

        public void Seek(TimeSpan time)
        {
            Calls.Add(string.Create(CultureInfo.InvariantCulture, $"seek {time.TotalSeconds}"));
            Time = time;
            TimeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Raise(OdMediaState state)
        {
            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Disposed = true;
    }
}
