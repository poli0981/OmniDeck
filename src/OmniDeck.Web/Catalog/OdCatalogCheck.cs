namespace OmniDeck.Web.Catalog;

/// <summary>What a daily catalog check found.</summary>
public enum OdCatalogCheck
{
    /// <summary><c>main</c>'s catalog has not changed since the last check.</summary>
    Unchanged,

    /// <summary>A newer, valid catalog was kept; it applies from the next start.</summary>
    Updated,

    /// <summary>The fetched catalog broke the rules and was not kept.</summary>
    Rejected,

    /// <summary>The check could not reach GitHub.</summary>
    Failed,
}
