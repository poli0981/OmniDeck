using System.Text.Json.Serialization;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Media;
using OmniDeck.Modules.Stocks;
using OmniDeck.Modules.Timer;
using OmniDeck.Modules.Weather;

namespace OmniDeck.Modules;

/// <summary>
/// Source-generated serializers for the modules' settings documents (the <c>module_settings</c> table)
/// and for what they keep in their own tables, such as Stocks' cached quotes.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OdClockSettings))]
[JsonSerializable(typeof(OdCalculatorSettings))]
[JsonSerializable(typeof(OdWeatherSettings))]
[JsonSerializable(typeof(OdMediaSettings))]
[JsonSerializable(typeof(OdStocksSettings))]
[JsonSerializable(typeof(OdAlarmsDocument))]
[JsonSerializable(typeof(OdQuote))]
[JsonSerializable(typeof(OdSeries))]
[JsonSerializable(typeof(OdCreditState))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class OdModulesJsonContext : JsonSerializerContext;
