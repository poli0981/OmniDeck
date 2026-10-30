using System.Diagnostics;
using System.Globalization;
using System.Windows.Threading;
using LibVLCSharp.WPF;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace MediaLab;

/// <summary>
/// Spike #6 (issue #7). A FluentWindow (Mica, custom title bar) hosting LibVLCSharp's VideoView with
/// WPF overlay controls, two fullscreen strategies, a pop-out, a tray-style hide, screen captures
/// (the only way to see airspace problems) and memory/CPU readings. `--auto` runs it all unattended.
/// </summary>
public partial class MainWindow
{
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDeck.Spikes");
    private static readonly string MediaDir = Path.Combine(DataRoot, "media");

    private readonly string _outDir = Path.Combine(DataRoot, "medialab", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
    private readonly DispatcherTimer _ui = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly StreamWriter _logFile;
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private FullscreenState? _saved;
    private Window? _otherWindow;
    private VideoView? _otherView;
    private bool _seeking;
    private int _captures;
    private (TimeSpan Cpu, DateTime Wall) _cpuMark;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_outDir);
        _logFile = new StreamWriter(Path.Combine(_outDir, "medialab.log")) { AutoFlush = true };
        Log($"MediaLab started; output in {_outDir}");
        LogMemory("before libVLC");
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _ui.Stop();
            _player?.Stop();
            _player?.Dispose();
            _libVlc?.Dispose();
            _logFile.Dispose();
        };
        DpiChanged += (_, e) => Log($"DpiChanged {e.OldDpi.PixelsPerDip:0.00} → {e.NewDpi.PixelsPerDip:0.00}");
        StateChanged += (_, _) => Log($"WindowState → {WindowState}");
    }

    private sealed record FullscreenState(
        System.Windows.WindowState State, double Left, double Top, double Width, double Height, bool Topmost,
        ResizeMode Resize, GridLength LogHeight, WindowCornerPreference Corners);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var sw = Stopwatch.StartNew();
        Core.Initialize();
        _libVlc = new LibVLC(enableDebugLogs: false);
        _libVlc.Log += (_, args) =>
        {
            if (args.Level >= LogLevel.Warning)
            {
                Dispatcher.InvokeAsync(() => Log($"libvlc {args.Level} [{args.Module}] {args.Message}"));
            }
        };
        _player = new MediaPlayer(_libVlc);
        _player.EncounteredError += (_, _) => Dispatcher.InvokeAsync(() => Log("MediaPlayer: EncounteredError"));
        Video.MediaPlayer = _player;
        Log($"libVLC {_libVlc.Version} ready in {sw.ElapsedMilliseconds} ms");
        LogMemory("libVLC idle");
        _ui.Tick += (_, _) => UpdateTime();
        _ui.Start();
        if (Environment.GetCommandLineArgs().Contains("--auto"))
        {
            Dispatcher.InvokeAsync(RunAutoAsync, DispatcherPriority.ContextIdle);
        }
        else if (Environment.GetCommandLineArgs().Contains("--auto-move"))
        {
            Dispatcher.InvokeAsync(RunMoveCheckAsync, DispatcherPriority.ContextIdle);
        }
    }

    // ---- Playback ------------------------------------------------------------------------------

    private void Play(string path)
    {
        using var media = new Media(_libVlc!, new Uri(path));
        _player!.Play(media);
        _cpuMark = (Process.GetCurrentProcess().TotalProcessorTime, DateTime.UtcNow);
        Log($"play {Path.GetFileName(path)}");
    }

    private void UpdateTime()
    {
        if (_player is not { Length: > 0 })
        {
            return;
        }

        TimeText.Text = $"{Fmt(_player.Time)} / {Fmt(_player.Length)}";
        if (!_seeking)
        {
            SeekSlider.Value = _player.Position;
        }

        static string Fmt(long ms) => TimeSpan.FromMilliseconds(ms).ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private void LogPlayback(string label)
    {
        var stats = _player!.Media?.Statistics;
        var process = Process.GetCurrentProcess();
        var (cpu0, wall0) = _cpuMark;
        var cpuPercent = (process.TotalProcessorTime - cpu0).TotalMilliseconds / (DateTime.UtcNow - wall0).TotalMilliseconds / Environment.ProcessorCount * 100;
        _cpuMark = (process.TotalProcessorTime, DateTime.UtcNow);
        _player.Size(0, ref Unsafe.Width, ref Unsafe.Height);
        Log($"{label}: playing={_player.IsPlaying} time={_player.Time} ms state={_player.State} video={Unsafe.Width}x{Unsafe.Height} "
            + $"displayed={stats?.DisplayedPictures} lost={stats?.LostPictures} cpu={cpuPercent:0.0}% of all cores");
    }

    // libVLC's Size() takes refs; a tiny holder keeps the call site readable.
    private static class Unsafe
    {
        public static uint Width;
        public static uint Height;
    }

    // ---- Fullscreen: in place (the shell window covers the monitor) -----------------------------

    private void ToggleFullscreenInPlace()
    {
        if (_saved is { } s)
        {
            WindowCornerPreference = s.Corners;
            Topmost = s.Topmost;
            ResizeMode = s.Resize;
            Left = s.Left;
            Top = s.Top;
            Width = s.Width;
            Height = s.Height;
            WindowState = s.State;
            LogRow.Height = s.LogHeight;
            TitleBarControl.Visibility = Visibility.Visible;
            LabBar.Visibility = Visibility.Visible;
            LogBox.Visibility = Visibility.Visible;
            _saved = null;
            Log("fullscreen in place: off");
            return;
        }

        _saved = new FullscreenState(WindowState, Left, Top, Width, Height, Topmost, ResizeMode, LogRow.Height, WindowCornerPreference);
        TitleBarControl.Visibility = Visibility.Collapsed;
        LabBar.Visibility = Visibility.Collapsed;
        LogBox.Visibility = Visibility.Collapsed;
        LogRow.Height = new GridLength(0);
        var px = Native.MonitorBounds(this);
        var dpi = VisualTreeHelper.GetDpi(this);
        WindowState = System.Windows.WindowState.Normal;
        WindowCornerPreference = WindowCornerPreference.DoNotRound;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Left = px.X / dpi.DpiScaleX;
        Top = px.Y / dpi.DpiScaleY;
        Width = px.Width / dpi.DpiScaleX;
        Height = px.Height / dpi.DpiScaleY;
        Log($"fullscreen in place: on, monitor {px.Width}x{px.Height} px at ({px.X},{px.Y}), scale {dpi.DpiScaleX:0.00}");
    }

    // ---- Fullscreen in its own window, and pop-out: the player moves to another VideoView -------

    private void OpenOtherWindow(bool fullscreen, bool restartVout)
    {
        if (_otherWindow is not null)
        {
            CloseOtherWindow(restartVout);
            return;
        }

        var view = new VideoView();
        var back = new Wpf.Ui.Controls.Button { Content = fullscreen ? "Exit fullscreen" : "Back to main window", Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        back.Click += (_, _) => CloseOtherWindow(restartVout);
        view.Content = new Grid { Children = { back } };
        _otherView = view;

        if (fullscreen)
        {
            var px = Native.MonitorBounds(this);
            var dpi = VisualTreeHelper.GetDpi(this);
            _otherWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                Background = Brushes.Black,
                Left = px.X / dpi.DpiScaleX,
                Top = px.Y / dpi.DpiScaleY,
                Width = px.Width / dpi.DpiScaleX,
                Height = px.Height / dpi.DpiScaleY,
                Content = view,
            };
        }
        else
        {
            _otherWindow = new FluentWindow
            {
                Title = "MediaLab pop-out",
                Width = 800,
                Height = 480,
                ExtendsContentIntoTitleBar = true,
                WindowBackdropType = WindowBackdropType.Mica,
                Content = new Grid
                {
                    RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() },
                    Children = { new TitleBar { Title = "MediaLab pop-out" }, view },
                },
            };
            Grid.SetRow(view, 1);
        }

        _otherWindow.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                CloseOtherWindow(restartVout);
            }
        };
        view.Loaded += (_, _) => MovePlayer(Video, view, restartVout);
        _otherWindow.Show();
        Log($"{(fullscreen ? "fullscreen window" : "pop-out")} opened (restart vout: {restartVout})");
    }

    private void CloseOtherWindow(bool restartVout)
    {
        if (_otherWindow is null || _otherView is null)
        {
            return;
        }

        MovePlayer(_otherView, Video, restartVout);
        _otherWindow.Close();
        _otherWindow = null;
        _otherView = null;
        Log("other window closed; player back in the main window");
    }

    private void MovePlayer(VideoView from, VideoView to, bool restartVout)
    {
        var time = _player!.Time;
        from.MediaPlayer = null;
        to.MediaPlayer = _player;
        if (restartVout && _player.VideoTrack is var track and >= 0)
        {
            // libVLC 3 keeps drawing into the window it started with; re-selecting the video track
            // makes it create a new video output on the new HWND. The decoder then restarts
            // mid-GOP, so seek to the current time to resume from a keyframe (with --seek-on-move).
            _player.SetVideoTrack(-1);
            _player.SetVideoTrack(track);
            if (Environment.GetCommandLineArgs().Contains("--seek-on-move"))
            {
                _player.Time = time;
            }
        }

        Log($"player moved at {time} ms (restart vout: {restartVout}); playing={_player.IsPlaying}");
    }

    // ---- Captures and readings -----------------------------------------------------------------

    private string Capture(string label, Window? window = null)
    {
        var path = Path.Combine(_outDir, $"{++_captures:00}-{label}.png");
        Native.CaptureScreen(Native.WindowBounds(window ?? this), path);
        Log($"captured {Path.GetFileName(path)}");
        return path;
    }

    private void LogMemory(string label)
    {
        using var p = Process.GetCurrentProcess();
        Log($"memory {label}: private commit {p.PrivateMemorySize64 / 1048576.0:F0} MB, working set {p.WorkingSet64 / 1048576.0:F0} MB");
    }

    private void Log(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";
        _logFile.WriteLine(stamped);
        LogBox.AppendText(stamped + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    // ---- Unattended run: `MediaLab.exe --auto` -----------------------------------------------------

    // `--auto-move`: only the player hand-over to another window, captured at 0.5 s and 2 s.
    private async Task RunMoveCheckAsync()
    {
        try
        {
            await Task.Delay(1000);
            Play(Path.Combine(MediaDir, "test-1080p30.mp4"));
            await Task.Delay(4500);
            OpenOtherWindow(fullscreen: true, restartVout: true);
            await Task.Delay(500);
            Capture("move-0.5s", _otherWindow);
            await Task.Delay(1500);
            Capture("move-2s", _otherWindow);
            LogPlayback("after the move");
            CloseOtherWindow(restartVout: true);
            await Task.Delay(500);
            Capture("move-back-0.5s");
        }
        catch (Exception ex)
        {
            Log($"MOVE FAILED: {ex}");
        }
        finally
        {
            await Task.Delay(500);
            Close();
        }
    }

    private async Task RunAutoAsync()
    {
        var media1080 = Path.Combine(MediaDir, "test-1080p30.mp4");
        var media2160 = Path.Combine(MediaDir, "test-2160p60.mp4");
        try
        {
            await Task.Delay(1000);
            Play(media1080);
            await Task.Delay(4000);
            LogPlayback("1080p30 in the window");
            LogMemory("1080p30 playing");
            Capture("1080p-window");

            _player!.AddSlave(MediaSlaveType.Subtitle, new Uri(Path.ChangeExtension(media1080, ".srt")).AbsoluteUri, true);
            await Task.Delay(2500);
            Capture("1080p-subtitle");

            ToggleFullscreenInPlace();
            await Task.Delay(2500);
            LogPlayback("fullscreen in place");
            Capture("fullscreen-in-place");
            ToggleFullscreenInPlace();
            await Task.Delay(2000);
            Capture("back-from-fullscreen");

            foreach (var restart in new[] { false, true })
            {
                OpenOtherWindow(fullscreen: true, restartVout: restart);
                await Task.Delay(3000);
                LogPlayback($"own fullscreen window, restart={restart}");
                Capture($"own-window-restart-{restart}", _otherWindow);
                CloseOtherWindow(restart);
                await Task.Delay(2500);
                LogPlayback($"back in the main window, restart={restart}");
                Capture($"back-from-own-window-restart-{restart}");
            }

            OpenOtherWindow(fullscreen: false, restartVout: true);
            await Task.Delay(3000);
            Capture("popout", _otherWindow);
            CloseOtherWindow(restartVout: true);
            await Task.Delay(2000);

            Hide();
            await Task.Delay(2000);
            Show();
            Activate();
            await Task.Delay(2000);
            LogPlayback("after hide/show");
            Capture("after-hide-show");

            Play(media2160);
            await Task.Delay(6000);
            LogPlayback("2160p60 in the window");
            LogMemory("2160p60 playing");
            Capture("2160p60-window");

            _player.Stop();
            await Task.Delay(1500);
            LogMemory("stopped");
        }
        catch (Exception ex)
        {
            Log($"AUTO FAILED: {ex}");
        }
        finally
        {
            await Task.Delay(500);
            Close();
        }
    }

    // ---- UI handlers ---------------------------------------------------------------------------

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        if (_player is null)
        {
            return;
        }

        if (_player.IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    private void OnSeekPressed(object sender, System.Windows.Input.MouseButtonEventArgs e) => _seeking = true;

    private void OnSeekReleased(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _seeking = false;
        if (_player is not null)
        {
            _player.Position = (float)SeekSlider.Value;
        }
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Media|*.mp4;*.mkv;*.webm;*.avi;*.mov;*.mp3;*.flac;*.m4a|All files|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            Play(dialog.FileName);
        }
    }

    private void OnOpen1080(object sender, RoutedEventArgs e) => Play(Path.Combine(MediaDir, "test-1080p30.mp4"));

    private void OnOpen2160(object sender, RoutedEventArgs e) => Play(Path.Combine(MediaDir, "test-2160p60.mp4"));

    private void OnAddSubtitle(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Subtitles|*.srt;*.ass;*.ssa;*.vtt", InitialDirectory = MediaDir };
        if (dialog.ShowDialog(this) == true)
        {
            _player?.AddSlave(MediaSlaveType.Subtitle, new Uri(dialog.FileName).AbsoluteUri, true);
            Log($"subtitle {Path.GetFileName(dialog.FileName)}");
        }
    }

    private void OnFullscreenInPlace(object sender, RoutedEventArgs e) => ToggleFullscreenInPlace();

    private void OnFullscreenWindow(object sender, RoutedEventArgs e) => OpenOtherWindow(fullscreen: true, restartVout: true);

    private void OnPopOut(object sender, RoutedEventArgs e) => OpenOtherWindow(fullscreen: false, restartVout: true);

    private async void OnHideShow(object sender, RoutedEventArgs e)
    {
        Hide();
        await Task.Delay(3000);
        Show();
        Activate();
        LogPlayback("after hide/show");
    }

    private void OnCapture(object sender, RoutedEventArgs e) => Capture("manual", _otherWindow);

    private void OnMemory(object sender, RoutedEventArgs e)
    {
        LogMemory("now");
        if (_player is not null)
        {
            LogPlayback("now");
        }
    }
}
