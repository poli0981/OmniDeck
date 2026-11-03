using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Weather;

/// <summary>A choice in the units list; null <see cref="Units"/> follows the region.</summary>
public sealed class OdWeatherUnitsOption(OdWeatherUnits? units, Func<string> label) : OdLocalizedOption(label)
{
    public OdWeatherUnits? Units { get; } = units;
}
