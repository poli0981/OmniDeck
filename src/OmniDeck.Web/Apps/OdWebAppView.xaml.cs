using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OmniDeck.Core.Localization;
using OmniDeck.Web.Navigation;
using OmniDeck.Web.Runtime;

namespace OmniDeck.Web.Apps;

/// <summary>
/// A web app's screen (docs/ARCHITECTURE.md §3): WebView2 on the app's catalog profile, with the
/// browser settings, the navigation policy, sign-in popups, crash recovery and the console capture.
/// </summary>
public partial class OdWebAppView : IOdWebSurface
{
    // Microsoft's page for the Evergreen WebView2 Runtime, which Windows 11 normally ships with.
    private static readonly Uri RuntimeDownload = new("https://developer.microsoft.com/microsoft-edge/webview2/");

    private readonly OdWebAppModule _module;
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
