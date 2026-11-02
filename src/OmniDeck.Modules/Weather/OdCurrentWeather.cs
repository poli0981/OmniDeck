namespace OmniDeck.Modules.Weather;

/// <summary>
/// Conditions now, in the place's local time. <c>Code</c> is the WMO weather code, and the wind direction is
/// where the wind blows from, in degrees.
/// </summary>
public sealed record OdCurrentWeather(
    DateTime Time,
    double Temperature,
    double FeelsLike,
    int Humidity,
    int Code,
    bool IsDay,
    double WindSpeed,
    int WindDirection);
