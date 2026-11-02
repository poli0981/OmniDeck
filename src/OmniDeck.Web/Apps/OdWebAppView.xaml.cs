using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Web.Navigation;
using OmniDeck.Web.Runtime;

namespace OmniDeck.Web.Apps;

/// <summary>
/// A web app's screen (docs/ARCHITECTURE.md §3): WebView2 on the app's catalog profile, with the
/// browser settings, the navigation policy, sign-in popups, notifications, permission questions,
/// full screen, crash recovery and the console capture.
/// </summary>
public partial class OdWebAppView : IOdWebSurface
{
    // Microsoft's page for the Evergreen WebView2 Runtime, which Windows 11 normally ships with.
    private static readonly Uri RuntimeDownload = new("https://developer.microsoft.com/microsoft-edge/webview2/");

    private readonly OdWebAppModule _module;
    private readonly Queue<OdPermissionQuestion> _questions = new();
    private WebView2? _browser;
    private bool _starting;
    private bool _loadedOnce;
    private DateTimeOffset? _lastFailure;
    private Action? _noticeAction;

    internal OdWebAppView(OdWebAppModule module)
    {
        InitializeComponent();
        _module = module;
        _module.Links.HeldBack += OnLinkHeldBack;
        Loaded += OnLoaded;
    }

    public Uri? Source => _browser?.CoreWebView2 is null ? null : _browser.Source;

    public bool IsPlayingAudio => _browser?.CoreWebView2?.IsDocumentPlayingAudio == true;

    public async Task SuspendAsync()
    {
        if (_browser?.CoreWebView2 is not { } core)
        {
            return;
        }

        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
        try
        {
            await core.TrySuspendAsync();
        }
        catch (InvalidOperationException ex)
        {
            // WebView2 refuses while the browser is still visible.
            _module.Log.Debug(ex, "The web app could not be suspended");
        }
    }

    public void Resume()
    {
        if (_browser?.CoreWebView2 is not { } core)
        {
            return;
        }

        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
        if (core.IsSuspended)
        {
            core.Resume();
        }
    }

    public async Task<int?> CountCookiesAsync(Uri page) =>
        _browser?.CoreWebView2 is { } core ? (await core.CookieManager.GetCookiesAsync(page.AbsoluteUri)).Count : null;

    public void Close()
    {
        _module.Links.HeldBack -= OnLinkHeldBack;
        Loaded -= OnLoaded;
        DisposeBrowser();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await StartAsync();

    private async Task StartAsync()
    {
        if (_starting || _browser is not null)
        {
            return;
        }

        if (OdWebRuntime.InstalledVersion() is null)
        {
            Starting.Visibility = Visibility.Collapsed;
            ShowNotice(OdLocalization.Get("Web_NoRuntime"), OdLocalization.Get("Web_GetRuntime"), () => OdExternalLinks.ShellOpen(RuntimeDownload));
            return;
        }

        _starting = true;
        Starting.Visibility = Visibility.Visible;
        try
        {
            var environment = await _module.Runtime.OpenAsync();
            var browser = new WebView2();
            BrowserHost.Children.Add(browser);
            _browser = browser;
            await browser.EnsureCoreWebView2Async(environment, OdWebRuntime.ControllerOptions(environment, _module.Entry.Profile));
            _module.Runtime.Track(_module.Info.Id, browser.CoreWebView2);
            Configure(browser.CoreWebView2);
            HideNotice();
            browser.CoreWebView2.Navigate(_module.StartPage.AbsoluteUri);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _module.Log.Error(ex, "The web app's browser could not start");
            DisposeBrowser();
            ShowNotice(OdLocalization.Format("Web_Crashed", _module.Entry.Name), OdLocalization.Get("Web_Reload"), Restart);
        }
        finally
        {
            _starting = false;
            Starting.Visibility = Visibility.Collapsed;
        }
    }

    private void Configure(CoreWebView2 core)
    {
        // docs/ARCHITECTURE.md §3: no DevTools in release builds, and the browser never keeps passwords
        // or form data; the console is captured through the protocol instead.
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.NavigationCompleted += OnNavigationCompleted;
        core.ProcessFailed += OnProcessFailed;
        core.ContainsFullScreenElementChanged += OnFullScreenElementChanged;
        core.NotificationReceived += OnNotificationReceived;
        core.PermissionRequested += OnPermissionRequested;
        _ = CaptureConsoleAsync(core);
    }

    private async Task CaptureConsoleAsync(CoreWebView2 core)
    {
        var console = _module.Console;
        core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled").DevToolsProtocolEventReceived += (_, e) => console.AddConsoleCall(e.ParameterObjectAsJson);
        core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived += (_, e) => console.AddException(e.ParameterObjectAsJson);
        core.GetDevToolsProtocolEventReceiver("Log.entryAdded").DevToolsProtocolEventReceived += (_, e) => console.AddLogEntry(e.ParameterObjectAsJson);
        try
        {
            await core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
            await core.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _module.Log.Warning(ex, "The web app's console capture could not start");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
        {
            e.Cancel = true;
            return;
        }

        var decision = _module.Policy.Navigate(target);
        if (decision != OdNavDecision.Allow)
        {
            e.Cancel = true;
            Leave(target, decision);
        }
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
        {
            e.Handled = true;
            return;
        }

        var decision = _module.Policy.Popup(target);
        if (decision != OdNavDecision.Allow)
        {
            e.Handled = true;
            Leave(target, decision);
            return;
        }

        // A sign-in popup opens in the app, on the same profile, so the sign-in lands in the app's cookies.
        var deferral = e.GetDeferral();
        var popup = new OdWebPopupWindow(_module) { Owner = Window.GetWindow(this) };
        try
        {
            e.NewWindow = await popup.OpenAsync();
            e.Handled = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _module.Log.Warning(ex, "A sign-in popup could not open; it goes to the browser");
            popup.Close();
            e.Handled = true;
            Leave(target, OdNavDecision.OpenExternally);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        try
        {
            await _module.FirstLoadAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _module.Log.Warning(ex, "The web app's sign-in health could not be checked");
        }
    }

    // A page's full screen (a video's, say) fills the monitor: the window covers it (OdFullscreen).
    private void OnFullScreenElementChanged(object? sender, object e)
    {
        if (sender is CoreWebView2 core)
        {
            OdFullscreen.Request(this, core.ContainsFullScreenElement);
        }
    }

    /// <summary>The window left full screen on its own (Esc, another module): the page leaves it too.</summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == OdFullscreen.IsActiveProperty && e.NewValue is false && _browser?.CoreWebView2 is { ContainsFullScreenElement: true } core)
        {
            _ = LeavePageFullScreenAsync(core);
        }
    }

    private async Task LeavePageFullScreenAsync(CoreWebView2 core)
    {
        try
        {
            await core.ExecuteScriptAsync("if (document.fullscreenElement) { document.exitFullscreen().catch(() => {}); }");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _module.Log.Debug(ex, "The page could not leave full screen");
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _module.Log.Warning("A web app process failed: {Kind} ({Reason})", e.ProcessFailedKind, e.Reason);

        // Frames, the GPU and helper processes recover on their own; the page and the browser do not.
        if (e.ProcessFailedKind is not (CoreWebView2ProcessFailedKind.RenderProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
            or CoreWebView2ProcessFailedKind.BrowserProcessExited))
        {
            return;
        }

        // docs/ARCHITECTURE.md §6: the first failure reloads the app by itself; another within a minute asks.
        var now = DateTimeOffset.UtcNow;
        var again = _lastFailure is { } last && now - last < TimeSpan.FromMinutes(1);
        _lastFailure = now;
        if (again)
        {
            _module.Remember(Source);
            DisposeBrowser();
            ShowNotice(OdLocalization.Format("Web_Crashed", _module.Entry.Name), OdLocalization.Get("Web_Reload"), Restart);
        }
        else
        {
            Restart();
        }
    }

    private void OnNotificationReceived(object? sender, CoreWebView2NotificationReceivedEventArgs e)
    {
        // OmniDeck shows the notification itself, or not at all: never in WebView2's own window.
        e.Handled = true;
        var notification = e.Notification;
        if (!_module.Notify(notification.Title, notification.Body, () => ReportClicked(notification)))
        {
            return;
        }

        try
        {
            notification.ReportShown();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _module.Log.Debug(ex, "A web notification could not be reported as shown");
        }
    }

    private void ReportClicked(CoreWebView2Notification notification)
    {
        try
        {
            notification.ReportClicked();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The page is gone: opening the app was all there was to do.
            _module.Log.Debug(ex, "A web notification's click could not be reported");
        }
    }

    private async void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        // OmniDeck keeps the answers (settings.json), so the profile does not, and asks each time.
        e.SavesInProfile = false;
        if (_module.Decide(e.PermissionKind) is { } state)
        {
            e.State = state;
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            e.State = await AskAsync(e.PermissionKind) ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
            deferral.Complete();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The browser closed while the question was open.
            _module.Log.Debug(ex, "A permission request outlived its browser");
        }
    }

    /// <summary>Asks the user, one question at a time; false when the browser closes first.</summary>
    private Task<bool> AskAsync(CoreWebView2PermissionKind kind)
    {
        var question = new OdPermissionQuestion(kind);
        _questions.Enqueue(question);
        if (_questions.Count == 1)
        {
            ShowQuestion();
        }

        return question.Answer.Task;
    }

    private void ShowQuestion()
    {
        // Another request for a kind just answered takes the same answer.
        while (_questions.TryPeek(out var next) && _module.Decide(next.Kind) is { } state)
        {
            _questions.Dequeue().Answer.TrySetResult(state == CoreWebView2PermissionState.Allow);
        }

        if (_questions.TryPeek(out var question))
        {
            Question.Message = OdWebPermissions.Question(question.Kind, _module.Entry.Name);
            QuestionPanel.Visibility = Visibility.Visible;
        }
        else
        {
            QuestionPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnAllow(object sender, RoutedEventArgs e) => Answer(allowed: true);

    private void OnBlock(object sender, RoutedEventArgs e) => Answer(allowed: false);

    private void Answer(bool allowed)
    {
        if (!_questions.TryDequeue(out var question))
        {
            return;
        }

        _module.RememberAnswer(question.Kind, allowed);
        question.Answer.TrySetResult(allowed);
        ShowQuestion();
    }

    private void Leave(Uri target, OdNavDecision decision)
    {
        if (decision == OdNavDecision.OpenExternally)
        {
            _module.Links.Open(target);
        }
        else
        {
            // The scheme only: the link itself may say who the user is.
            _module.Log.Information("Blocked a {Scheme} link", target.Scheme);
        }
    }

    private void Restart()
    {
        _module.Remember(Source);
        DisposeBrowser();
        _ = StartAsync();
    }

    private void DisposeBrowser()
    {
        if (_browser is not { } browser)
        {
            return;
        }

        _browser = null;
        while (_questions.TryDequeue(out var question))
        {
            question.Answer.TrySetResult(false);
        }

        QuestionPanel.Visibility = Visibility.Collapsed;
        if (browser.CoreWebView2 is { } core)
        {
            _module.Runtime.Forget(core);
        }

        BrowserHost.Children.Remove(browser);
        browser.Dispose();
        _module.Runtime.Close();
    }

    private void OnLinkHeldBack(object? sender, OdLinkEventArgs e) =>
        ShowNotice(OdLocalization.Format("Web_LinkHeldBack", _module.Entry.Name), OdLocalization.Get("Web_Dismiss"), action: null);

    private void ShowNotice(string message, string actionText, Action? action)
    {
        Notice.Message = message;
        NoticeAction.Content = actionText;
        _noticeAction = action;
        NoticePanel.Visibility = Visibility.Visible;
    }

    private void HideNotice()
    {
        NoticePanel.Visibility = Visibility.Collapsed;
        _noticeAction = null;
    }

    private void OnNoticeAction(object sender, RoutedEventArgs e)
    {
        var action = _noticeAction;
        HideNotice();
        action?.Invoke();
    }
}
