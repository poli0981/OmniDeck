namespace OmniDeck.Web.Catalog;

/// <summary>One web app in the catalog (docs/WEBAPP-CATALOG.md §1).</summary>
public sealed record OdCatalogEntry
{
    /// <summary>The stable id, <c>[a-z0-9-]</c>; also the module id and settings key.</summary>
    public required string Id { get; init; }

    /// <summary>The app's name, shown as written.</summary>
    public required string Name { get; init; }

    /// <summary>Where the app starts; HTTPS only.</summary>
    public required string StartUrl { get; init; }

    /// <summary>The cookie and profile group; entries sharing a value share sign-in.</summary>
    public required string Profile { get; init; }

    /// <summary>Domains the main frame may navigate to.</summary>
    public required IReadOnlyList<string> NavAllow { get; init; }

    /// <summary>Names of catalog-level domain sets the main frame may also navigate to; null when absent.</summary>
    public IReadOnlyList<string>? NavAllowSets { get; init; }

    /// <summary>Domains whose popups open in-app on the same profile (sign-in flows).</summary>
    public required IReadOnlyList<string> PopupAllow { get; init; }

    /// <summary>Whether the app's web notifications become Windows notifications.</summary>
    public required bool Notifications { get; init; }

    /// <summary>Whether the governor keeps the app running while it plays audio.</summary>
    public required bool AudioPin { get; init; }
}
