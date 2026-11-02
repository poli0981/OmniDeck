using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>Conditions now, as the screen shows them.</summary>
public sealed record OdWeatherNow(string Temperature, string Condition, SymbolRegular Symbol, string FeelsLike, string Humidity, string Wind);
