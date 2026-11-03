namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Market data (docs/SPEC.md §3.5). The key comes with every call, from the secret store, so the
/// provider never keeps it; failures throw <see cref="OdStocksException"/>.
/// </summary>
public interface IOdQuoteProvider
{
    /// <summary>The quotes of up to a minute's credits' worth of listings; one credit each.</summary>
    Task<OdQuoteBatch> QuotesAsync(IReadOnlyList<OdTicker> tickers, string key, CancellationToken cancellationToken);

    /// <summary>A listing's bars over <paramref name="range"/>; one credit.</summary>
    Task<OdSeries> SeriesAsync(OdTicker ticker, OdSeriesRange range, string key, CancellationToken cancellationToken);

    /// <summary>The listings that match <paramref name="text"/>, with the plan each needs; free.</summary>
    Task<IReadOnlyList<OdListing>> SearchAsync(string text, string key, CancellationToken cancellationToken);

    /// <summary>The key's plan, limits and use so far, which also proves the key works; one credit.</summary>
    Task<OdUsage> UsageAsync(string key, CancellationToken cancellationToken);
}
