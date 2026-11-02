using System.Globalization;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// A place the user saved, as Open-Meteo's geocoding found it: its name, its first-level region (a
/// province or state) and its country, either of which may be empty, and its coordinates.
/// </summary>
public sealed record OdWeatherLocation(string Name, string Region, string Country, double Latitude, double Longitude)
{
    /// <summary>Identifies the place in the cache: its coordinates, to Open-Meteo's precision.</summary>
    public string Key => string.Create(CultureInfo.InvariantCulture, $"{Latitude:F4},{Longitude:F4}");

    /// <summary>Name, region and country, skipping parts that are empty or repeat the name.</summary>
    public string Description => string.Join(", ", new[] { Name, Region, Country }.Where(part => part.Length > 0).Distinct(StringComparer.Ordinal));

    /// <summary>What screen readers announce for the place in a list.</summary>
    public override string ToString() => Description;
}
