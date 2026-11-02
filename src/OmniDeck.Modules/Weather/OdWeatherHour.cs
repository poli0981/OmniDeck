using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>One hour of the forecast, as the screen shows it.</summary>
public sealed record OdWeatherHour(string Time, string Temperature, SymbolRegular Symbol, string Condition, string Chance);
