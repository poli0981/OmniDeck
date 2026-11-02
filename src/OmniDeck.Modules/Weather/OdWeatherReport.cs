namespace OmniDeck.Modules.Weather;

/// <summary>A forecast to show, when it was fetched, and whether a newer one could not be fetched.</summary>
public sealed record OdWeatherReport(OdForecast Forecast, DateTimeOffset FetchedAt, bool IsStale);
