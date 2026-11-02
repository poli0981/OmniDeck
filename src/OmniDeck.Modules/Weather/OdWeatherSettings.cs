namespace OmniDeck.Modules.Weather;

/// <summary>The weather module's saved places and its units.</summary>
public sealed record OdWeatherSettings
{
    /// <summary>The saved places, in the order the user added them.</summary>
    public IReadOnlyList<OdWeatherLocation> Locations { get; init; } = [];

    /// <summary>The index of the place on screen; out of range shows the first.</summary>
    public int Selected { get; init; }

    /// <summary>Null follows the regional format.</summary>
    public OdWeatherUnits? Units { get; init; }
}
