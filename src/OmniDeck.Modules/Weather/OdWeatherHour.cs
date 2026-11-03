using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>One hour of the forecast, as the screen shows it.</summary>
public sealed record OdWeatherHour(string Time, string Temperature, SymbolRegular Symbol, string Condition, string Chance)
{
    /// <summary>The hour as one sentence: screen readers name a list item after its data.</summary>
    public override string ToString() => OdLocalization.Format("Weather_HourName", Time, Temperature, Condition, Chance);
}
