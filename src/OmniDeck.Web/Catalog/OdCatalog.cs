using System.Text.Json.Serialization;

namespace OmniDeck.Web.Catalog;

/// <summary>
/// The web-app catalog, <c>catalog/webapps.json</c> (docs/WEBAPP-CATALOG.md): the only web apps
/// OmniDeck runs, and how each is sandboxed.
/// </summary>
public sealed record OdCatalog
{
    /// <summary>The schema version this build understands.</summary>
    public const int SupportedSchemaVersion = 2;

    /// <summary>An editor hint; the app ignores it.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    public required int SchemaVersion { get; init; }

    /// <summary>The date of the last content change, <c>yyyy-MM-dd</c>.</summary>
    public required string Updated { get; init; }

    /// <summary>Named lists of domains that entries add to their <c>navAllow</c> by name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> NavAllowSets { get; init; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public required IReadOnlyList<OdCatalogEntry> Apps { get; init; }

    /// <summary>A catalog with no apps, for a build whose catalogs all failed to load.</summary>
    public static OdCatalog Empty { get; } = new() { SchemaVersion = SupportedSchemaVersion, Updated = "0001-01-01", Apps = [] };
}
