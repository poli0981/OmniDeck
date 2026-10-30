using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows.Threading;

namespace WebLab;

public sealed record Target(string Label, string Url, string Profile, CatalogEntry? Entry);

/// <summary>
/// The WebView2 lab for spikes #1–#4. One shared environment, one profile per catalog profile group,
/// the catalog's navigation policy, the governor's lifecycle steps, a per-process memory sampler and
/// CDP console capture. `--auto` runs the unattended checks; `--wv2-args=...` passes browser arguments.
/// </summary>
public partial class MainWindow
{
    private const string LabHost = "weblab.example";
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OmniDeck.Spikes");
    private static readonly string LabPages = Path.Combine(AppContext.BaseDirectory, "LabPages");

    private readonly ObservableCollection<Slot> _slots = [];
    private readonly DispatcherTimer _sampler = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly string _stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
    private readonly StreamWriter _logFile;
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> _waiters = [];
    private CoreWebView2Environment? _env;
    private StreamWriter? _csv;
    private string _phase = "-";
    private bool _sampling;
    private bool _browserGone;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(Path.Combine(DataRoot, "weblab"));
        _logFile = new StreamWriter(Path.Combine(DataRoot, "weblab", $"{_stamp}.log")) { AutoFlush = true };
        SlotList.ItemsSource = _slots;
        Targets.ItemsSource = BuildTargets();
        Targets.SelectedIndex = 0;
        _sampler.Tick += async (_, _) => await SampleAsync();
        _sampler.Start();
        Closed += (_, _) =>
        {
            _csv?.Dispose();
            _logFile.Dispose();
        };

        Log($"WebLab started. Windows {Environment.OSVersion.Version}, {Environment.ProcessorCount} logical CPUs, "
            + $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024):F1} GiB RAM. Data under {DataRoot}");
        if (Environment.GetCommandLineArgs().Contains("--auto"))
        {
            Loaded += async (_, _) => await RunAutoAsync();
        }
        else if (Environment.GetCommandLineArgs().Contains("--measure"))
        {
            Loaded += async (_, _) => await RunMeasureAsync();
        }
        else if (Arg("--cookie-set") is not null || Environment.GetCommandLineArgs().Contains("--cookie-check"))
        {
            Loaded += async (_, _) => await RunCookieProbeAsync();
        }
        else if (Environment.GetCommandLineArgs().Contains("--check-signed-in"))
        {
            Loaded += async (_, _) => await RunSignedInCheckAsync();
        }
        else if (Arg("--ladder") is { } ladderApps)
        {
            Loaded += async (_, _) => await RunLaddersOnlyAsync(ladderApps);
        }
    }

    private Slot? Selected => SlotList.SelectedItem as Slot;

    private static List<Target> BuildTargets()
    {
        var targets = new List<Target>
        {
            new("lab · EME probe (#1)", $"https://{LabHost}/eme.html", "lab", null),
            new("lab · Shaka support page (#1)", "https://shaka-player-demo.appspot.com/support.html", "lab", null),
            new("lab · CDP detection (#4)", $"https://{LabHost}/cdp-detect.html", "lab", null),
        };
        targets.AddRange(Catalog.Load().Select(e => new Target($"{e.Id} · {e.Name} [{e.Profile}]", e.StartUrl, e.Profile, e)));
        return targets;
    }

    private static string? Arg(string name) =>
        Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];

    // ---- Environment and views -------------------------------------------------------------------

    private async Task<CoreWebView2Environment> EnsureEnvironmentAsync()
    {
        if (_env is not null)
        {
            return _env;
        }

        var args = Arg("--wv2-args");
        var options = new CoreWebView2EnvironmentOptions(additionalBrowserArguments: args);
        _env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(DataRoot, "WebView2"), options);
        Log($"Environment: WebView2 Runtime {_env.BrowserVersionString}; user data {_env.UserDataFolder}; browser args '{args ?? ""}'");
        EnvInfo.Text = $"WebView2 Runtime {_env.BrowserVersionString}";
        return _env;
    }

    private async Task<WebView2> CreateViewAsync(Slot slot, string url, Action<CoreWebView2>? beforeNavigate = null)
    {
        var env = await EnsureEnvironmentAsync();
        var view = new WebView2();
        Host.Children.Add(view);
        var options = env.CreateCoreWebView2ControllerOptions();
        options.ProfileName = slot.Profile;
        await view.EnsureCoreWebView2Async(env, options);
        Configure(view.CoreWebView2, slot);
        beforeNavigate?.Invoke(view.CoreWebView2);
        view.CoreWebView2.Navigate(url);
        return view;
    }

    private void Configure(CoreWebView2 core, Slot slot)
    {
        // docs/ARCHITECTURE.md §3 per-app pipeline. The user agent is never touched.
        core.Settings.AreDevToolsEnabled = DevToolsEnabled.IsChecked == true;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.SetVirtualHostNameToFolderMapping(LabHost, LabPages, CoreWebView2HostResourceAccessKind.Allow);

        core.NavigationStarting += (_, e) =>
        {
            var host = Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) ? u.Host : "";
            var allowed = slot.Entry is null || Catalog.Matches(host, slot.Entry.NavAllow);
            Log($"[{slot.Id}] {(e.IsRedirected ? "redirect" : "navigate")} {Catalog.Redact(e.Uri)}"
                + $"{(e.IsUserInitiated ? " (user)" : "")}{(allowed ? "" : " — OUTSIDE navAllow")}");
            if (!allowed && EnforcePolicy.IsChecked == true)
            {
                e.Cancel = true;
                Log($"[{slot.Id}]   cancelled: the product would open this in the default browser");
            }
        };
        core.NavigationCompleted += (_, e) =>
            Log($"[{slot.Id}] completed {(e.IsSuccess ? "ok" : e.WebErrorStatus.ToString())} {Catalog.Redact(core.Source)}");
        core.NewWindowRequested += (_, e) => OnNewWindowRequested(slot, e);
        core.IsDocumentPlayingAudioChanged += (_, _) => Log($"[{slot.Id}] playing audio: {core.IsDocumentPlayingAudio}");
        core.ProcessFailed += (_, e) => Log($"[{slot.Id}] ProcessFailed {e.ProcessFailedKind}, reason {e.Reason}, exit {e.ExitCode}");
        core.PermissionRequested += (_, e) => Log($"[{slot.Id}] permission {e.PermissionKind} for {Catalog.Redact(e.Uri)} (default handling)");
        core.WebMessageReceived += (_, e) => OnWebMessage(slot, e.WebMessageAsJson);
    }

    private async void OnNewWindowRequested(Slot slot, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var host = Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) ? u.Host : "";
        var inApp = slot.Entry is null || EnforcePolicy.IsChecked != true || Catalog.Matches(host, slot.Entry.PopupAllow);
        Log($"[{slot.Id}] popup {Catalog.Redact(e.Uri)} → "
            + (inApp ? "in-app popup on the same profile" : "blocked: the product would open it in the default browser"));
        if (!inApp)
        {
            e.Handled = true;
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var view = new WebView2();
            var features = e.WindowFeatures;
            var window = new Window
            {
                Title = $"{slot.Id} · popup",
                Owner = this,
                Content = view,
                Width = features.HasSize ? features.Width : 520,
                Height = features.HasSize ? features.Height : 700,
            };
            window.Show();
            var options = _env!.CreateCoreWebView2ControllerOptions();
            options.ProfileName = slot.Profile;
            await view.EnsureCoreWebView2Async(_env, options);
            Configure(view.CoreWebView2, new Slot { Id = slot.Id + ":popup", Profile = slot.Profile, StartUrl = e.Uri, Entry = slot.Entry });
            view.CoreWebView2.WindowCloseRequested += (_, _) => window.Close();
            window.Closed += (_, _) => view.Dispose();
            e.NewWindow = view.CoreWebView2;
            e.Handled = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnWebMessage(Slot slot, string json)
    {
        Log($"[{slot.Id}] page says: {json}");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("probe", out var probe)
            && _waiters.Remove(probe.GetString() ?? "", out var waiter))
        {
            waiter.TrySetResult(doc.RootElement.Clone());
        }
    }

    private Task<JsonElement> WaitForMessageAsync(string probe)
    {
        var tcs = new TaskCompletionSource<JsonElement>();
        _waiters[probe] = tcs;
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    // ---- Governor lifecycle steps ----------------------------------------------------------------

    private async Task<Slot> OpenAsync(Target target)
    {
        var baseId = target.Entry?.Id ?? "lab";
        var n = _slots.Count(s => s.Id == baseId || s.Id.StartsWith(baseId + "#", StringComparison.Ordinal));
        var slot = new Slot { Id = n == 0 ? baseId : $"{baseId}#{n + 1}", Profile = target.Profile, StartUrl = target.Url, Entry = target.Entry };
        _slots.Add(slot);
        var sw = Stopwatch.StartNew();
        slot.View = await CreateViewAsync(slot, target.Url);
        slot.State = "Active";
        SlotList.SelectedItem = slot;
        ShowOnly(slot);
        Log($"[{slot.Id}] opened on profile '{slot.Profile}', controller ready in {sw.ElapsedMilliseconds} ms");
        return slot;
    }

    private void ShowOnly(Slot slot)
    {
        foreach (var s in _slots.Where(s => s.View is not null))
        {
            var visible = ReferenceEquals(s, slot);
            s.View!.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
            {
                s.State = "Active";
            }
            else if (s.State == "Active")
            {
                s.State = "Hidden";
            }
        }

        SlotList.Items.Refresh();
    }

    private void Hide(Slot slot)
    {
        slot.View!.Visibility = Visibility.Collapsed;
        slot.State = "Hidden";
        SlotList.Items.Refresh();
    }

    private async Task SuspendAsync(Slot slot)
    {
        var core = slot.View!.CoreWebView2;
        Hide(slot);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background); // let IsVisible reach the controller
        var sw = Stopwatch.StartNew();
        var ok = await core.TrySuspendAsync();
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
        slot.State = ok ? "Suspended" : "Hidden (suspend refused)";
        SlotList.Items.Refresh();
        Log($"[{slot.Id}] TrySuspendAsync → {ok} in {sw.ElapsedMilliseconds} ms; MemoryUsageTargetLevel = Low");
    }

    private void Resume(Slot slot)
    {
        var core = slot.View!.CoreWebView2;
        var sw = Stopwatch.StartNew();
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
        core.Resume();
        ShowOnly(slot);
        Log($"[{slot.Id}] resumed (Normal + Resume + show) in {sw.Elapsed.TotalMilliseconds:F1} ms");
    }

    private void Hibernate(Slot slot)
    {
        if (slot.View is null)
        {
            return;
        }

        slot.SavedUrl = slot.View.CoreWebView2?.Source ?? slot.StartUrl;
        Host.Children.Remove(slot.View);
        slot.View.Dispose();
        slot.View = null;
        slot.ConsoleReceiver = null;
        slot.LogReceiver = null;
        slot.Capturing = false;
        slot.State = "Hibernated";
        SlotList.Items.Refresh();
        Log($"[{slot.Id}] disposed; kept {{url}} = {Catalog.Redact(slot.SavedUrl)}");
    }

    private async Task ReopenAsync(Slot slot)
    {
        if (slot.View is not null)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        var first = new TaskCompletionSource<bool>();
        slot.View = await CreateViewAsync(slot, slot.SavedUrl ?? slot.StartUrl,
            core => core.NavigationCompleted += (_, e) => first.TrySetResult(e.IsSuccess));
        var controllerMs = sw.ElapsedMilliseconds;
        ShowOnly(slot);
        var ok = await first.Task.WaitAsync(TimeSpan.FromSeconds(60));
        Log($"[{slot.Id}] reopened: controller {controllerMs} ms, first NavigationCompleted ({(ok ? "ok" : "failed")}) {sw.ElapsedMilliseconds} ms");
    }

    // ---- CDP console capture (spike #4) ----------------------------------------------------------

    private async Task SetCaptureAsync(Slot slot, bool runtime, bool log)
    {
        if (slot.View?.CoreWebView2 is not { } core)
        {
            return;
        }

        if (slot.ConsoleReceiver is null)
        {
            slot.ConsoleReceiver = core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled");
            slot.ConsoleReceiver.DevToolsProtocolEventReceived += (_, e) => AddConsole(slot, "console", e.ParameterObjectAsJson);
            slot.LogReceiver = core.GetDevToolsProtocolEventReceiver("Log.entryAdded");
            slot.LogReceiver.DevToolsProtocolEventReceived += (_, e) => AddConsole(slot, "log", e.ParameterObjectAsJson);
            core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived +=
                (_, e) => AddConsole(slot, "exception", e.ParameterObjectAsJson);
        }

        var sw = Stopwatch.StartNew();
        await core.CallDevToolsProtocolMethodAsync(runtime ? "Runtime.enable" : "Runtime.disable", "{}");
        await core.CallDevToolsProtocolMethodAsync(log ? "Log.enable" : "Log.disable", "{}");
        slot.Capturing = runtime || log;
        Log($"[{slot.Id}] console capture: Runtime {(runtime ? "on" : "off")}, Log {(log ? "on" : "off")} in {sw.ElapsedMilliseconds} ms "
            + $"(DevTools UI {(core.Settings.AreDevToolsEnabled ? "enabled" : "disabled")})");
    }

    private void AddConsole(Slot slot, string source, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var text = source switch
        {
            "console" => $"{Str(root, "type")}: " + string.Join(" ", root.GetProperty("args").EnumerateArray()
                .Select(a => a.TryGetProperty("value", out var v) ? v.ToString() : Str(a, "description"))),
            "log" => $"[{Str(root.GetProperty("entry"), "source")}/{Str(root.GetProperty("entry"), "level")}] {Str(root.GetProperty("entry"), "text")}",
            _ => $"uncaught: {Str(root.GetProperty("exceptionDetails"), "text")} "
                + (root.GetProperty("exceptionDetails").TryGetProperty("exception", out var ex) ? Str(ex, "description") : ""),
        };
        slot.AddConsole($"{DateTime.Now:HH:mm:ss.fff} {source,-9} {text}");
        if (ReferenceEquals(slot, Selected))
        {
            ConsoleBox.Text = string.Join(Environment.NewLine, slot.ConsoleRing);
            ConsoleBox.ScrollToEnd();
        }

        static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "";
    }

    // ---- Memory sampling (spike #3) --------------------------------------------------------------

    private async Task SampleAsync()
    {
        if (_sampling)
        {
            return;
        }

        _sampling = true;
        try
        {
            var rows = new List<ProcessSample>();
            if (MemorySampler.Sample(Environment.ProcessId, "Shell", "shell") is { } shell)
            {
                rows.Add(shell);
            }

            if (_env is not null)
            {
                try
                {
                    var owners = await AttributeAsync();
                    foreach (var info in _env.GetProcessInfos())
                    {
                        if (MemorySampler.Sample(info.ProcessId, info.Kind.ToString(), owners.GetValueOrDefault(info.ProcessId, "shared")) is { } row)
                        {
                            rows.Add(row);
                        }
                    }

                    _browserGone = false;
                }
                catch (Exception ex) when (ex is FileNotFoundException or System.Runtime.InteropServices.COMException or InvalidOperationException)
                {
                    // With every controller closed the browser process exits; the environment object stays.
                    if (!_browserGone)
                    {
                        Log($"No WebView2 browser process ({ex.GetType().Name}: {ex.Message.Trim()})");
                        _browserGone = true;
                    }
                }
            }

            foreach (var slot in _slots)
            {
                slot.AttributedMb = rows.Where(r => r.Attributed.Split('+').Contains(slot.Id)).Sum(r => r.PrivateCommitMb);
            }

            MemGrid.ItemsSource = rows;
            Totals.Text = $"{rows.Count} processes · Σ private commit {rows.Sum(r => r.PrivateCommitMb):F0} MB · "
                + $"Σ private WS {rows.Sum(r => r.PrivateWsMb):F0} MB · Σ WS {rows.Sum(r => r.WsMb):F0} MB · phase '{_phase}'";
            if (_csv is not null)
            {
                var time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                foreach (var r in rows)
                {
                    _csv.WriteLine(string.Join(',', time, _phase, r.Pid, r.Kind, r.Attributed.Replace(',', ';'),
                        F(r.PrivateCommitMb), F(r.PrivateWsMb), F(r.WsMb), F(r.SharedCommitMb), F(r.CpuMs)));
                }
            }

            SlotList.Items.Refresh();
        }
        finally
        {
            _sampling = false;
        }

        static string F(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    }

    /// <summary>Renderer and OOPIF processes → slot, by each frame's root (main frame) id, then by host.</summary>
    private async Task<Dictionary<int, string>> AttributeAsync()
    {
        var owners = new Dictionary<int, string>();
        foreach (var info in await _env!.GetProcessExtendedInfosAsync())
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var frame in info.AssociatedFrameInfos)
            {
                var root = frame;
                while (root.ParentFrameInfo is not null)
                {
                    root = root.ParentFrameInfo;
                }

                names.Add(OwnerOf(root));
            }

            if (names.Count > 0)
            {
                owners[info.ProcessInfo.ProcessId] = string.Join('+', names);
            }
            else if (info.ProcessInfo.Kind == CoreWebView2ProcessKind.Renderer)
            {
                owners[info.ProcessInfo.ProcessId] = "renderer (no frames)";
            }
        }

        return owners;
    }

    private string OwnerOf(CoreWebView2FrameInfo root)
    {
        var rootHost = Uri.TryCreate(root.Source, UriKind.Absolute, out var u) ? u.Host : "";
        Slot? byHost = null;
        foreach (var slot in _slots)
        {
            try
            {
                if (slot.View?.CoreWebView2 is not { } core)
                {
                    continue;
                }

                var sameHost = Uri.TryCreate(core.Source, UriKind.Absolute, out var s) && s.Host == rootHost;
                if (sameHost && core.FrameId == root.FrameId)
                {
                    return slot.Id;
                }

                byHost ??= sameHost ? slot : null;
            }
            catch (InvalidOperationException)
            {
                // The view is being torn down.
            }
        }

        return byHost?.Id ?? $"?{rootHost}";
    }

    private void Mark(string phase)
    {
        _phase = phase;
        Log($"PHASE → {phase}");
    }

    private void StartCsv()
    {
        if (_csv is not null)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(DataRoot, "memory"));
        var file = Path.Combine(DataRoot, "memory", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-samples.csv");
        _csv = new StreamWriter(file) { AutoFlush = true };
        _csv.WriteLine("time,phase,pid,kind,attributed,private_commit_mb,private_ws_mb,ws_mb,shared_commit_mb,cpu_ms");
        RecordButton.Content = "Stop CSV";
        Log($"CSV → {file}");
    }

    private void StopCsv()
    {
        _csv?.Dispose();
        _csv = null;
        RecordButton.Content = "Start CSV";
        Log("CSV stopped");
    }

    private async Task RunLadderAsync(Slot slot, TimeSpan phase)
    {
        StartCsv();
        Mark($"{slot.Id}:active");
        ShowOnly(slot);
        await Task.Delay(phase);
        Mark($"{slot.Id}:hidden");
        Hide(slot);
        await Task.Delay(phase / 2);
        Mark($"{slot.Id}:suspended");
        await SuspendAsync(slot);
        await Task.Delay(phase);
        Mark($"{slot.Id}:resumed");
        Resume(slot);
        await Task.Delay(phase / 2);
        Mark($"{slot.Id}:hibernated");
        Hibernate(slot);
        await Task.Delay(phase / 2);
        Mark($"{slot.Id}:reopened");
        await ReopenAsync(slot);
        await Task.Delay(phase);
        Mark("-");
        Log($"[{slot.Id}] ladder done");
    }

    // ---- Unattended checks: `WebLab.exe --auto` ---------------------------------------------------

    private async Task RunAutoAsync()
    {
        var targets = (List<Target>)Targets.ItemsSource;
        try
        {
            StartCsv();
            Mark("auto:shell-only");
            await Task.Delay(TimeSpan.FromSeconds(6));

            // Spike #1: which key systems the runtime exposes.
            Mark("auto:eme");
            var emeWait = WaitForMessageAsync("eme");
            var eme = await OpenAsync(targets[0]);
            var emeResult = await emeWait;
            foreach (var r in emeResult.GetProperty("results").EnumerateArray())
            {
                Log($"EME {r.GetProperty("keySystem").GetString()} [{r.GetProperty("robustness").GetString()}]: "
                    + $"{(r.GetProperty("available").GetBoolean() ? "AVAILABLE" : "no")} — {r.GetProperty("detail").GetString()}");
            }

            // Spike #4: detection and capture with DevTools UI disabled (the release setting).
            Mark("auto:cdp");
            var cdpWait = WaitForMessageAsync("cdp-detect");
            var cdp = await OpenAsync(targets[2]);
            Log($"CDP detect, capture off: {(await cdpWait).GetProperty("detected")}");
            foreach (var (runtime, log, label) in new[] { (true, true, "Runtime+Log"), (false, true, "Log only"), (false, false, "off") })
            {
                await SetCaptureAsync(cdp, runtime, log);
                var before = cdp.ConsoleRing.Count;
                var wait = WaitForMessageAsync("cdp-detect");
                await cdp.View!.CoreWebView2.ExecuteScriptAsync(
                    "check(); console.log('ring-test', 42); console.warn('ring-warn'); setTimeout(() => { throw new Error('ring-throw'); });"
                    + "fetch('https://weblab.example/missing.json').catch(() => {});");
                var detected = (await wait).GetProperty("detected");
                await Task.Delay(1000);
                Log($"CDP detect, capture {label}: {detected}; ring buffer +{cdp.ConsoleRing.Count - before} entries");
            }

            Log("Ring buffer contents:" + Environment.NewLine + string.Join(Environment.NewLine, cdp.ConsoleRing));

            // Spike #3 dry run: environment baseline and a short ladder on a public page (not signed in).
            Hibernate(eme);
            Hibernate(cdp);
            Mark("auto:env-only");
            await Task.Delay(TimeSpan.FromSeconds(10));
            var youtube = targets.FirstOrDefault(t => t.Entry?.Id == "youtube");
            if (youtube is not null)
            {
                await RunLadderAsync(await OpenAsync(youtube), TimeSpan.FromSeconds(20));
            }
        }
        catch (Exception ex)
        {
            Log($"AUTO FAILED: {ex}");
        }
        finally
        {
            StopCsv();
            await Task.Delay(500);
            Close();
        }
    }

    // ---- UI handlers -----------------------------------------------------------------------------

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        if (Targets.SelectedItem is Target target)
        {
            await OpenAsync(target);
        }
    }

    private void OnShow(object sender, RoutedEventArgs e)
    {
        if (Selected is { View: not null } slot)
        {
            ShowOnly(slot);
        }
    }

    private void OnHide(object sender, RoutedEventArgs e)
    {
        if (Selected is { View: not null } slot)
        {
            Hide(slot);
        }
    }

    private async void OnSuspend(object sender, RoutedEventArgs e)
    {
        if (Selected is { View: not null } slot)
        {
            await SuspendAsync(slot);
        }
    }

    private void OnResume(object sender, RoutedEventArgs e)
    {
        if (Selected is { View: not null } slot)
        {
            Resume(slot);
        }
    }

    private void OnHibernate(object sender, RoutedEventArgs e)
    {
        if (Selected is { } slot)
        {
            Hibernate(slot);
        }
    }

    private async void OnReopen(object sender, RoutedEventArgs e)
    {
        if (Selected is { View: null } slot)
        {
            await ReopenAsync(slot);
        }
    }

    private async void OnCaptureFull(object sender, RoutedEventArgs e)
    {
        if (Selected is { } slot)
        {
            await SetCaptureAsync(slot, runtime: true, log: true);
        }
    }

    private async void OnCaptureLogOnly(object sender, RoutedEventArgs e)
    {
        if (Selected is { } slot)
        {
            await SetCaptureAsync(slot, runtime: false, log: true);
        }
    }

    private async void OnCaptureOff(object sender, RoutedEventArgs e)
    {
        if (Selected is { } slot)
        {
            await SetCaptureAsync(slot, runtime: false, log: false);
        }
    }

    private void OnCloseSlot(object sender, RoutedEventArgs e)
    {
        if (Selected is { } slot)
        {
            Hibernate(slot);
            _slots.Remove(slot);
        }
    }

    private async void OnLadder(object sender, RoutedEventArgs e)
    {
        if (Selected is not { View: not null } slot)
        {
            Log("Select an open slot first.");
            return;
        }

        var seconds = int.TryParse(PhaseSeconds.Text, out var s) && s > 0 ? s : 60;
        await RunLadderAsync(slot, TimeSpan.FromSeconds(seconds));
    }

    private void OnHibernateAll(object sender, RoutedEventArgs e)
    {
        foreach (var slot in _slots)
        {
            Hibernate(slot);
        }
    }

    private void OnRecord(object sender, RoutedEventArgs e)
    {
        if (_csv is null)
        {
            StartCsv();
        }
        else
        {
            StopCsv();
        }
    }

    private void OnMark(object sender, RoutedEventArgs e) => Mark(string.IsNullOrWhiteSpace(MarkText.Text) ? "-" : MarkText.Text.Trim());

    private void OnSlotSelected(object sender, SelectionChangedEventArgs e) =>
        ConsoleBox.Text = Selected is { } slot ? string.Join(Environment.NewLine, slot.ConsoleRing) : "";

    private void Log(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";
        _logFile.WriteLine(stamped);
        LogBox.AppendText(stamped + Environment.NewLine);
        LogBox.ScrollToEnd();
    }
}
