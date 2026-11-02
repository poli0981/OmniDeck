using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;

namespace OmniDeck.App.Settings;

/// <summary>One web app in Settings → Web apps: its notifications switch and the permissions it was answered.</summary>
public sealed partial class OdWebAppRowViewModel(OdCatalogEntry entry) : ObservableObject
{
    [ObservableProperty]
    private bool _notifications;

    [ObservableProperty]
    private bool _hasPermissions;

    [ObservableProperty]
    private string _permissionsText = "";

    public OdCatalogEntry Entry => entry;

    public string Name => entry.Name;

    public void Refresh(bool notifications, IReadOnlyDictionary<string, bool> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        Notifications = notifications;
        HasPermissions = permissions.Count > 0;
        PermissionsText = permissions.Count == 0
            ? OdLocalization.Get("WebApps_NoPermissions")
            : string.Join(" · ", permissions.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => Answer(pair.Key, pair.Value)));
    }

    public override string ToString() => Name;

    private static string Answer(string kind, bool allowed) => allowed
        ? OdLocalization.Format("WebApps_Allowed", OdWebPermissions.Name(kind))
        : OdLocalization.Format("WebApps_Blocked", OdWebPermissions.Name(kind));
}
