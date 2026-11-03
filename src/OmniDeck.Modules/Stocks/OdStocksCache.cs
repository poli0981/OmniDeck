using System.Data;
using System.Globalization;
using System.Text.Json;
using Dapper;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The last quote of each listing and its last series per range, in <c>stocks_quotes_cache</c> and
/// <c>stocks_series_cache</c>: the module opens at once and keeps showing figures when Twelve Data is
/// out of reach. Nothing is deleted on a failure; only removing a listing forgets it.
/// </summary>
public sealed class OdStocksCache(OdDatabase database)
{
    public OdCached<OdQuote>? Quote(OdTicker ticker)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        using var connection = database.Open();
        using var reader = connection.ExecuteReader("SELECT fetched_at, payload FROM stocks_quotes_cache WHERE ticker = @key", new { key = ticker.Key });
        return Read(reader, payload => JsonSerializer.Deserialize(payload, OdModulesJsonContext.Default.OdQuote));
    }

    public void PutQuote(OdQuote quote, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(quote);
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO stocks_quotes_cache (ticker, fetched_at, payload) VALUES (@key, @fetchedAt, @payload) "
            + "ON CONFLICT (ticker) DO UPDATE SET fetched_at = excluded.fetched_at, payload = excluded.payload",
            new { key = quote.Ticker.Key, fetchedAt = Format(fetchedAt), payload = JsonSerializer.Serialize(quote, OdModulesJsonContext.Default.OdQuote) });
    }

    public OdCached<OdSeries>? Series(OdTicker ticker, OdSeriesRange range)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        using var connection = database.Open();
        using var reader = connection.ExecuteReader("SELECT fetched_at, payload FROM stocks_series_cache WHERE series_key = @key", new { key = SeriesKey(ticker, range) });
        return Read(reader, payload => JsonSerializer.Deserialize(payload, OdModulesJsonContext.Default.OdSeries));
    }

    public void PutSeries(OdSeries series, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(series);
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO stocks_series_cache (series_key, fetched_at, payload) VALUES (@key, @fetchedAt, @payload) "
            + "ON CONFLICT (series_key) DO UPDATE SET fetched_at = excluded.fetched_at, payload = excluded.payload",
            new { key = SeriesKey(series.Ticker, series.Range), fetchedAt = Format(fetchedAt), payload = JsonSerializer.Serialize(series, OdModulesJsonContext.Default.OdSeries) });
    }

    /// <summary>Forgets a listing the user took off the watchlist.</summary>
    public void Forget(OdTicker ticker)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        using var connection = database.Open();
        connection.Execute("DELETE FROM stocks_quotes_cache WHERE ticker = @key", new { key = ticker.Key });
        connection.Execute("DELETE FROM stocks_series_cache WHERE series_key LIKE @prefix", new { prefix = ticker.Key + "|%" });
    }

    /// <summary>The row the reader is on, or null; a payload that no longer reads counts as nothing cached.</summary>
    private static OdCached<T>? Read<T>(IDataReader reader, Func<string, T?> deserialize)
        where T : class
    {
        if (!reader.Read())
        {
            return null;
        }

        try
        {
            return deserialize(reader.GetString(1)) is { } value
                ? new OdCached<T>(value, DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SeriesKey(OdTicker ticker, OdSeriesRange range) => ticker.Key + "|" + range;

    private static string Format(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);
}
