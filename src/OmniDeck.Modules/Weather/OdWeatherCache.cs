using System.Globalization;
using Dapper;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// The last forecast for each place and units, as Open-Meteo sent it, in the <c>weather_cache</c>
/// table: the module opens at once, and shows the last forecast when Open-Meteo is out of reach.
/// </summary>
public sealed class OdWeatherCache(OdDatabase database)
{
    public OdCachedWeather? Get(string key)
    {
        using var connection = database.Open();
        using var reader = connection.ExecuteReader("SELECT fetched_at, payload FROM weather_cache WHERE cache_key = @key", new { key });
        return reader.Read()
            ? new OdCachedWeather(DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), reader.GetString(1))
            : null;
    }

    public void Put(string key, OdCachedWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO weather_cache (cache_key, fetched_at, payload) VALUES (@key, @fetchedAt, @payload) "
            + "ON CONFLICT (cache_key) DO UPDATE SET fetched_at = excluded.fetched_at, payload = excluded.payload",
            new { key, fetchedAt = weather.FetchedAt.ToString("O", CultureInfo.InvariantCulture), payload = weather.Payload });
    }

    /// <summary>Forgets a place's forecasts, in every unit.</summary>
    public void Forget(OdWeatherLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        using var connection = database.Open();
        connection.Execute("DELETE FROM weather_cache WHERE cache_key LIKE @prefix", new { prefix = location.Key + "|%" });
    }
}
