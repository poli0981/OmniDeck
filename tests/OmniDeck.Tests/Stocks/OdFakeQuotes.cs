using OmniDeck.Modules.Charts;
using OmniDeck.Modules.Stocks;

namespace OmniDeck.Tests.Stocks;

/// <summary>Market data without the network: every listing costs 10, unless a test says otherwise; calls are recorded.</summary>
public sealed class OdFakeQuotes : IOdQuoteProvider
{
    public IList<string> Calls { get; } = [];

    /// <summary>Listings that fail on their own, by ticker key.</summary>
    public IDictionary<string, OdStocksError> Unavailable { get; } = new Dictionary<string, OdStocksError>(StringComparer.Ordinal);

    /// <summary>Makes every call fail as a whole.</summary>
    public OdStocksError? Fails { get; set; }

    public OdUsage Usage { get; set; } = new(1, 8, 10, 800, "basic");

    public IReadOnlyList<OdListing> Listings { get; set; } = [];

    public Task<OdQuoteBatch> QuotesAsync(IReadOnlyList<OdTicker> tickers, string key, CancellationToken cancellationToken)
    {
        Calls.Add("quote:" + string.Join(',', tickers.Select(ticker => ticker.Key)));
        ThrowIfFailing();
        var quotes = tickers.Where(ticker => !Unavailable.ContainsKey(ticker.Key)).ToDictionary(ticker => ticker.Key, Quote, StringComparer.Ordinal);
        var failed = tickers.Where(ticker => Unavailable.ContainsKey(ticker.Key)).ToDictionary(ticker => ticker.Key, ticker => Unavailable[ticker.Key], StringComparer.Ordinal);
        return Task.FromResult(new OdQuoteBatch(quotes, failed));
    }

    public Task<OdSeries> SeriesAsync(OdTicker ticker, OdSeriesRange range, string key, CancellationToken cancellationToken)
    {
        Calls.Add("series:" + ticker.Key);
        ThrowIfFailing();
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);
        IReadOnlyList<OdOhlc> bars = [.. Enumerable.Range(0, 30).Select(i => new OdOhlc(start.AddDays(i), 9 + (i * 0.05), 10, 8.5, 9 + (i * 0.05), 1000))];
        return Task.FromResult(new OdSeries(ticker, range, "USD", bars));
    }

    public Task<IReadOnlyList<OdListing>> SearchAsync(string text, string key, CancellationToken cancellationToken)
    {
        Calls.Add("search:" + text);
        ThrowIfFailing();
        return Task.FromResult(Listings);
    }

    public Task<OdUsage> UsageAsync(string key, CancellationToken cancellationToken)
    {
        Calls.Add("usage");
        ThrowIfFailing();
        return Task.FromResult(Usage);
    }

    private static OdQuote Quote(OdTicker ticker) =>
        new(ticker, ticker.Symbol + " Inc", "NASDAQ", "USD", 10m, 9.5m, 10.5m, 9.4m, 9.8m, 0.2m, 2.04m, 1000, 8m, 12m, true, null);

    private void ThrowIfFailing()
    {
        if (Fails is { } error)
        {
            throw new OdStocksException(error);
        }
    }
}
