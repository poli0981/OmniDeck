using OmniDeck.Core.Settings;
using OmniDeck.Web.Catalog;

namespace OmniDeck.Web.Apps;

/// <summary>
/// The user's choices for each web app, kept in settings.json (docs/legal/PRIVACY.md §1): whether it
/// shows notifications, as the catalog says until the user decides, and the site permissions it was
/// given or refused.
/// </summary>
public sealed class OdWebPreferences(IOdSettingsService settings)
{
    /// <summary>Whether <paramref name="entry"/> shows its notifications: the user's choice, else the catalog's.</summary>
    public bool NotificationsOn(OdCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return settings.Current.Web.For(entry.Id).Notifications ?? entry.Notifications;
    }

    public void SetNotifications(OdCatalogEntry entry, bool on)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Change(entry.Id, app => app with { Notifications = on });
    }

    /// <summary>The answers the user gave <paramref name="appId"/>, by permission kind.</summary>
    public IReadOnlyDictionary<string, bool> Permissions(string appId) => settings.Current.Web.For(appId).Permissions;

    /// <summary>Keeps the user's answer for one kind of permission.</summary>
    public void Remember(string appId, string kind, bool allowed) =>
        Change(appId, app => app with { Permissions = new Dictionary<string, bool>(app.Permissions, StringComparer.Ordinal) { [kind] = allowed } });

    /// <summary>Forgets every answer <paramref name="appId"/> got, so it asks again.</summary>
    public void ResetPermissions(string appId) => Change(appId, app => app with { Permissions = OdWebAppSettings.NoPermissions });

    private void Change(string appId, Func<OdWebAppSettings, OdWebAppSettings> change) =>
        settings.Update(s => s with { Web = s.Web.With(appId, change) });
}
