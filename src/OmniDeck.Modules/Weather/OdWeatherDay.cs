using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>One day of the forecast, as the screen shows it.</summary>
public sealed record OdWeatherDay(string Day, string HighLow, SymbolRegular Symbol, string Condition, string Chance);
