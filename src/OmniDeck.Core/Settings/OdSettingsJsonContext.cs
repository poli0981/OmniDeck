using System.Text.Json.Serialization;

namespace OmniDeck.Core.Settings;

/// <summary>Source-generated serializer for <see cref="OdSettings"/>: no reflection, readable output.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OdSettings))]
internal sealed partial class OdSettingsJsonContext : JsonSerializerContext;
