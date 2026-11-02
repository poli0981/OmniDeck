namespace OmniDeck.Modules.Weather;

/// <summary>A choice in the units list; a null <paramref name="Units"/> follows the region.</summary>
public sealed record OdWeatherUnitsOption(OdWeatherUnits? Units, string Label)
{
    /// <summary>What screen readers announce for the choice.</summary>
    public override string ToString() => Label;
}
