using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>One day of the forecast, as the screen shows it.</summary>
public sealed record OdWeatherDay(string Day, string HighLow, SymbolRegular Symbol, string Condition, string Chance)
{
    /// <summary>The day as one sentence: screen readers name a list item after its data.</summary>
    public override string ToString() => OdLocalization.Format("Weather_DayName", Day, HighLow, Condition, Chance);
}
