namespace OmniDeck.Modules.Weather;

/// <summary>A place's weather: now, the next 48 hours and the next 7 days.</summary>
public sealed record OdForecast(OdCurrentWeather Current, IReadOnlyList<OdHourlyWeather> Hourly, IReadOnlyList<OdDailyWeather> Daily, OdWeatherUnits Units);
