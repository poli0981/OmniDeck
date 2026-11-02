namespace OmniDeck.Modules.Weather;

/// <summary>A forecast as Open-Meteo sent it, and when it arrived.</summary>
public sealed record OdCachedWeather(DateTimeOffset FetchedAt, string Payload);
