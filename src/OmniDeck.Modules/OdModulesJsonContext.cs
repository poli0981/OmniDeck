using System.Text.Json.Serialization;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Media;
using OmniDeck.Modules.Weather;

namespace OmniDeck.Modules;

/// <summary>Source-generated serializers for the modules' settings documents (the <c>module_settings</c> table).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OdClockSettings))]
[JsonSerializable(typeof(OdCalculatorSettings))]
[JsonSerializable(typeof(OdWeatherSettings))]
[JsonSerializable(typeof(OdMediaSettings))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class OdModulesJsonContext : JsonSerializerContext;
