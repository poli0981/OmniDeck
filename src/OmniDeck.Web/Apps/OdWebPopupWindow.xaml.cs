using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OmniDeck.Web.Navigation;
using OmniDeck.Web.Runtime;

namespace OmniDeck.Web.Apps;

/// <summary>
/// A sign-in popup (docs/WEBAPP-CATALOG.md §1, <c>popupAllow</c>): a window of its own on the web app's
/// profile, under the same navigation policy, which closes when the page asks it to.
/// </summary>
public partial class OdWebPopupWindow
{
    private readonly OdWebAppModule _module;
    private WebView2? _browser;

    internal OdWebPopupWindow(OdWebAppModule module)
    {
        InitializeComponent();
        _module = module;
        Title = module.Entry.Name;
        PopupTitleBar.Title = module.Entry.Name;
        Closed += OnClosed;
    }

    /// <summary>Shows the window and returns its browser, ready for the page that asked for the popup.</summary>
    internal async Task<CoreWebView2> OpenAsync()
    {
        Show();
        var environment = await _module.Runtime.OpenAsync();
        var browser = new WebView2();
        BrowserHost.Children.Add(browser);
        _browser = browser;
        await browser.EnsureCoreWebView2Async(environment, OdWebRuntime.ControllerOptions(environment, _module.Entry.Profile));
        var core = browser.CoreWebView2;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.WindowCloseRequested += (_, _) => Close();
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        return core;
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
            if (decision == OdNavDecision.OpenExternally)
            {
                _module.Links.Open(target);
            }
        }
    }

    // A popup's own popups go to the browser.
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && _module.Policy.Popup(target) != OdNavDecision.Block)
        {
            _module.Links.Open(target);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_browser is not { } browser)
        {
            return;
        }

        _browser = null;
        BrowserHost.Children.Remove(browser);
        browser.Dispose();
        _module.Runtime.Close();
    }
}
