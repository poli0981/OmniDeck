using System.Net.Http;
using System.Text.Json;
using Serilog;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// Forecasts through the cache. A forecast younger than <see cref="FreshFor"/> is shown as it is; an
/// older one is fetched again, and when that fails the cached one is shown as stale. A failed request
/// is never retried on its own (docs/spikes/07-providers.md).
/// </summary>
public sealed class OdWeatherService(HttpClient http, OdWeatherCache cache, TimeProvider time, ILogger log)
{
    /// <summary>How long a forecast is shown without asking Open-Meteo again.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(30);

    /// <summary>Places matching <paramref name="name"/>, named in <paramref name="language"/> where Open-Meteo can.</summary>
    public async Task<IReadOnlyList<OdWeatherLocation>> SearchAsync(string name, string language, CancellationToken cancellationToken)
    {
        var json = await http.GetStringAsync(OdOpenMeteo.SearchUri(name, language), cancellationToken);
        return OdOpenMeteo.ParseSearch(json);
    }

    /// <summary>
    /// A place's forecast, fetched again unless the cached one is fresh and <paramref name="refresh"/>
    /// is false; null when there is nothing to show, with nothing cached and Open-Meteo out of reach.
    /// </summary>
    public async Task<OdWeatherReport?> ForecastAsync(OdWeatherLocation location, OdWeatherUnits units, bool refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        var key = OdOpenMeteo.CacheKey(location, units);
        var cached = cache.Get(key);
        var now = time.GetUtcNow();
        if (!refresh && cached is not null && now - cached.FetchedAt < FreshFor && Parse(cached, units) is { } fresh)
        {
            return new OdWeatherReport(fresh, cached.FetchedAt, IsStale: false);
        }

        try
        {
            var json = await http.GetStringAsync(OdOpenMeteo.ForecastUri(location, units), cancellationToken);
            var forecast = OdOpenMeteo.ParseForecast(json, units);
            cache.Put(key, new OdCachedWeather(now, json));
            return new OdWeatherReport(forecast, now, IsStale: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The message names neither the place nor its coordinates: logs can end up in a bug report.
            log.Warning(ex, "The weather could not be fetched");
            return cached is not null && Parse(cached, units) is { } last ? new OdWeatherReport(last, cached.FetchedAt, IsStale: true) : null;
        }
    }

    /// <summary>Forgets a removed place's cached forecasts.</summary>
    public void Forget(OdWeatherLocation location) => cache.Forget(location);

    private static OdForecast? Parse(OdCachedWeather cached, OdWeatherUnits units)
    {
        try
        {
            return OdOpenMeteo.ParseForecast(cached.Payload, units);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
