using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmniDeck.Web.Catalog;

/// <summary>
/// Reads the catalog strictly, as its JSON Schema does: an unknown property, a missing required one or
/// a null where a value belongs is an error.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    AllowTrailingCommas = false)]
[JsonSerializable(typeof(OdCatalog))]
internal sealed partial class OdCatalogJson : JsonSerializerContext
{
    /// <summary>Reads a catalog from its JSON text.</summary>
    /// <exception cref="JsonException">The text is not a catalog.</exception>
    public static OdCatalog Parse(string json) =>
        JsonSerializer.Deserialize(json, Default.OdCatalog) ?? throw new JsonException("The catalog is null.");
}
