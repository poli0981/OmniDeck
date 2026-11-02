using OmniDeck.Web.Catalog;

namespace OmniDeck.Web.Navigation;

/// <summary>
/// Where one web app may go (docs/WEBAPP-CATALOG.md §1): its main frame stays on the catalog's domains
/// (<c>navAllow</c> and its named sets) over HTTPS; web and mail links elsewhere open in the default
/// browser; anything else goes nowhere. Frames inside a page are never filtered.
/// </summary>
public sealed class OdNavPolicy
{
    private readonly IReadOnlyList<string> _allowed;
    private readonly IReadOnlyList<string> _popups;

    public OdNavPolicy(OdCatalogEntry entry, OdCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(catalog);
        var sets = (entry.NavAllowSets ?? []).SelectMany(set => catalog.NavAllowSets.TryGetValue(set, out var domains) ? domains : []);
        _allowed = [.. entry.NavAllow, .. sets];
        _popups = entry.PopupAllow;
    }

    /// <summary>Where a main-frame navigation to <paramref name="target"/> goes.</summary>
    public OdNavDecision Navigate(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Is(target, Uri.UriSchemeHttps))
        {
            return OdDomains.Covers(target.Host, _allowed) ? OdNavDecision.Allow : OdNavDecision.OpenExternally;
        }

        // about:blank and documents built in memory stay; a page that drops to plain HTTP leaves the app,
        // so the browser can warn about it.
        return target.Scheme switch
        {
            "about" or "data" or "blob" => OdNavDecision.Allow,
            _ => Outside(target),
        };
    }

    /// <summary>
    /// Where a popup to <paramref name="target"/> goes: sign-in pages (<c>popupAllow</c>) open in the app on
    /// the same profile, other web and mail links in the default browser.
    /// </summary>
    public OdNavDecision Popup(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Is(target, Uri.UriSchemeHttps) && OdDomains.Covers(target.Host, _popups) ? OdNavDecision.Allow : Outside(target);
    }

    private static OdNavDecision Outside(Uri target) =>
        Is(target, Uri.UriSchemeHttps) || Is(target, Uri.UriSchemeHttp) || Is(target, Uri.UriSchemeMailto)
            ? OdNavDecision.OpenExternally
            : OdNavDecision.Block;

    private static bool Is(Uri target, string scheme) => string.Equals(target.Scheme, scheme, StringComparison.Ordinal);
}
