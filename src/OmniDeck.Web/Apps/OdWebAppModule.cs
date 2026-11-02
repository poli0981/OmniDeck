using System.Windows;
using Microsoft.Web.WebView2.Core;
using OmniDeck.Core.Modules;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Navigation;
using OmniDeck.Web.Runtime;
using Serilog;
using Serilog.Core;

namespace OmniDeck.Web.Apps;

/// <summary>
/// One catalog web app as a module (docs/ARCHITECTURE.md §3). Suspending freezes its page and lowers its
/// memory; hibernating closes its browser but keeps the page it was on, and cookies stay in the profile,
/// so the app comes back signed in where it was. Its console log lives here, not in the view. With
/// notifications on it stays live while hidden, so it can notify (docs/RAM-GOVERNOR.md §3).
/// </summary>
public sealed class OdWebAppModule : IOdModule, IOdResidency
{
    private readonly Func<OdWebAppModule, FrameworkElement> _views;
    private IOdModuleContext? _context;
    private IOdWebSurface? _surface;
    private Uri? _lastPage;

    public OdWebAppModule(OdCatalogEntry entry, OdCatalog catalog, OdWebRuntime runtime, OdSignInHealth health, OdWebPreferences preferences, TimeProvider time)
        : this(entry, catalog, runtime, health, preferences, time, OdExternalLinks.ShellOpen, module => new OdWebAppView(module))
    {
    }

    internal OdWebAppModule(
        OdCatalogEntry entry,
        OdCatalog catalog,
        OdWebRuntime runtime,
        OdSignInHealth health,
        OdWebPreferences preferences,
        TimeProvider time,
        Action<Uri> openExternally,
        Func<OdWebAppModule, FrameworkElement> views)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        Runtime = runtime;
        Health = health;
        Preferences = preferences;
        _views = views;
        Info = new OdModuleInfo(entry.Id, NameKey: "", Icon: "Globe24", CategoryKey: "Category_Web", OdRamClass.Heavy, DisplayName: entry.Name);
        Policy = new OdNavPolicy(entry, catalog);
        Links = new OdExternalLinks(time, openExternally);
    }

    public OdModuleInfo Info { get; }

    public OdCatalogEntry Entry { get; }

    /// <summary>What the page wrote to its console this session; it outlives the view.</summary>
    public OdConsoleLog Console { get; } = new();

    internal OdWebRuntime Runtime { get; }

    internal OdSignInHealth Health { get; }

    internal OdWebPreferences Preferences { get; }

    internal OdNavPolicy Policy { get; }

    internal OdExternalLinks Links { get; }

    internal ILogger Log => _context?.Log ?? Logger.None;

    /// <summary>An app that shows notifications stays live while hidden: suspended, it could not notify.</summary>
    public bool StaysResident => Preferences.NotificationsOn(Entry);

    /// <summary>Where the browser opens: the page shown before hibernating, otherwise the start page.</summary>
    internal Uri StartPage => _lastPage ?? new Uri(Entry.StartUrl);

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var view = _views(this);
        _surface = (IOdWebSurface)view;
        return view;
    }

    public Task ActivateAsync()
    {
        _surface?.Resume();
        return Task.CompletedTask;
    }

    /// <summary>An app the catalog pins for audio stays while it plays (docs/RAM-GOVERNOR.md §2).</summary>
    public Task<bool> CanSuspendAsync() => Task.FromResult(!(Entry.AudioPin && _surface?.IsPlayingAudio == true));

    public Task SuspendAsync(OdSuspendReason reason) => _surface?.SuspendAsync() ?? Task.CompletedTask;

    public async Task HibernateAsync()
    {
        _lastPage = _surface?.Source ?? _lastPage;
        await ReleaseAsync();
    }

    public async Task CloseAsync()
    {
        _lastPage = null;
        await ReleaseAsync();
    }

    /// <summary>Keeps <paramref name="page"/> as the page to come back to, as when the view restarts its browser.</summary>
    internal void Remember(Uri? page)
    {
        if (page is not null)
        {
            _lastPage = page;
        }
    }

    /// <summary>
    /// Shows one of the page's notifications as a Windows notification; <paramref name="clicked"/> runs
    /// when it is clicked, after the app opens. False when the app's notifications are off.
    /// </summary>
    internal bool Notify(string? title, string? body, Action clicked)
    {
        if (_context is not { } context || !Preferences.NotificationsOn(Entry))
        {
            return false;
        }

        context.Notifier.Notify(OdWebNotifications.From(Entry.Name, title, body, clicked));
        return true;
    }

    /// <summary>The answer to a permission request, or null when the user has to be asked.</summary>
    internal CoreWebView2PermissionState? Decide(CoreWebView2PermissionKind kind) =>
        OdWebPermissions.Decide(kind, Preferences.NotificationsOn(Entry), Preferences.Permissions(Entry.Id).TryGetValue(kind.ToString(), out var allowed) ? allowed : null);

    /// <summary>Keeps the user's answer to a permission question.</summary>
    internal void RememberAnswer(CoreWebView2PermissionKind kind, bool allowed)
    {
        Preferences.Remember(Entry.Id, kind.ToString(), allowed);
        Log.Information("The user {Answer} {Kind} for {App}", allowed ? "allowed" : "blocked", kind, Entry.Id);
    }

    /// <summary>Called by the view when its first page loads: checks the profile's sign-in health once a session.</summary>
    internal async Task FirstLoadAsync()
    {
        if (Health.NeedsCheck(Entry.Profile) && _surface is { } surface && await surface.CountCookiesAsync(new Uri(Entry.StartUrl)) is { } count)
        {
            Health.AtFirstLoad(Entry.Profile, count);
        }
    }

    private async Task ReleaseAsync()
    {
        if (_surface is not { } surface)
        {
            return;
        }

        _surface = null;
        if (await surface.CountCookiesAsync(new Uri(Entry.StartUrl)) is { } count)
        {
            Health.AtClose(Entry.Profile, count);
        }

        surface.Close();
    }
}
