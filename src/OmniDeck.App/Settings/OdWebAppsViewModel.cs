using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using Serilog;

namespace OmniDeck.App.Settings;

/// <summary>
/// Settings → Web apps: each catalog app's notifications switch and the permissions it was answered,
/// the sign-in data each profile group keeps, and the catalog this session runs. Used from the UI thread.
/// </summary>
public sealed partial class OdWebAppsViewModel : ObservableObject, IDisposable
{
    private readonly OdCatalogService _catalog;
    private readonly OdWebPreferences _preferences;
    private readonly IOdSettingsService _settings;
    private readonly OdModuleHost _host;
    private readonly OdSignInHealth _health;
    private readonly Func<string, Task> _clearProfile;
    private readonly Func<string, Task<bool>> _confirmClear;
    private readonly ILogger _log;

    [ObservableProperty]
    private string _catalogText = "";

    [ObservableProperty]
    private string _checkedText = "";

    [ObservableProperty]
    private string _message = "";

    /// <summary>The page over the session's catalog.</summary>
    /// <param name="catalog">The session's catalog, and when main's copy was last checked.</param>
    /// <param name="preferences">The user's choices for each app.</param>
    /// <param name="settings">Where those choices are kept, to follow changes made in the apps.</param>
    /// <param name="host">Closes a group's apps before their data is cleared.</param>
    /// <param name="health">Told that a cleared profile comes back empty on purpose.</param>
    /// <param name="clearProfile">Deletes a catalog profile's data.</param>
    /// <param name="confirmClear">Asks before clearing, with the group's app names; false to stop.</param>
    /// <param name="log">The app log.</param>
    public OdWebAppsViewModel(
        OdCatalogService catalog,
        OdWebPreferences preferences,
        IOdSettingsService settings,
        OdModuleHost host,
        OdSignInHealth health,
        Func<string, Task> clearProfile,
        Func<string, Task<bool>> confirmClear,
        ILogger log)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);
        _catalog = catalog;
        _preferences = preferences;
        _settings = settings;
        _host = host;
        _health = health;
        _clearProfile = clearProfile;
        _confirmClear = confirmClear;
        _log = log.ForContext<OdWebAppsViewModel>();
        var apps = catalog.Current.Apps;
        Apps = [.. apps.Select(entry => new OdWebAppRowViewModel(entry))];
        Profiles =
        [
            .. apps.GroupBy(entry => entry.Profile, StringComparer.Ordinal)
                .Select(group => new OdWebProfileGroup(group.Key, [.. group.Select(entry => entry.Id)], string.Join(", ", group.Select(entry => entry.Name)))),
        ];
        settings.Changed += OnSettingsChanged;
        Refresh();
    }

    public IReadOnlyList<OdWebAppRowViewModel> Apps { get; }

    public IReadOnlyList<OdWebProfileGroup> Profiles { get; }

    public bool HasApps => Apps.Count > 0;

    /// <summary>Reads the choices and the catalog check again; the settings page calls it whenever the tab opens.</summary>
    public void Refresh()
    {
        foreach (var row in Apps)
        {
            row.Refresh(_preferences.NotificationsOn(row.Entry), _preferences.Permissions(row.Entry.Id));
        }

        CatalogText = OdLocalization.Format("WebApps_Catalog", _catalog.Current.Updated, Apps.Count);
        CheckedText = _catalog.LastChecked is { } checkedAt
            ? OdLocalization.Format("WebApps_Checked", checkedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
            : OdLocalization.Get("WebApps_NotChecked");
    }

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    [RelayCommand]
    private void ToggleNotifications(OdWebAppRowViewModel row) => _preferences.SetNotifications(row.Entry, !_preferences.NotificationsOn(row.Entry));

    [RelayCommand]
    private void ResetPermissions(OdWebAppRowViewModel row) => _preferences.ResetPermissions(row.Entry.Id);

    /// <summary>Signs out of every app in the group: closes them, then deletes the profile's data.</summary>
    [RelayCommand]
    private async Task ClearDataAsync(OdWebProfileGroup group)
    {
        Message = "";
        if (!await _confirmClear(group.AppNames))
        {
            return;
        }

        try
        {
            foreach (var id in group.AppIds)
            {
                await _host.CloseAsync(id);
            }

            await _clearProfile(group.Profile);
            _health.Forget(group.Profile);
            Message = OdLocalization.Format("WebApps_Cleared", group.AppNames);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Warning(ex, "Web profile {Profile} could not be cleared", group.Profile);
            Message = OdLocalization.Get("WebApps_ClearFailed");
        }
    }

    private void OnSettingsChanged(object? sender, OdSettingsChangedEventArgs e) => Refresh();
}
