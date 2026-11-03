namespace OmniDeck.Modules.Stocks;

/// <summary>
/// How long market data stays good, so credits are spent only when a figure can have moved. A quote is
/// fresh for the refresh interval while its market trades; after the close, one more fetch picks up
/// the final price, and that one lasts until the next session. A series lasts until half an hour after
/// the next close, since its bars change only once a day.
/// </summary>
public static class OdStocksFreshness
{
    /// <summary>How long after a close the final price is fetched, once.</summary>
    public static readonly TimeSpan AfterClose = TimeSpan.FromMinutes(20);

    /// <summary>How long after a close a day's bar is taken as final.</summary>
    public static readonly TimeSpan SeriesGrace = TimeSpan.FromMinutes(30);

    /// <summary>How long a quote from a market without a schedule here lasts while that market is closed.</summary>
    public static readonly TimeSpan UnknownMarketClosed = TimeSpan.FromHours(1);

    /// <summary>How long a series from a market without a schedule here lasts.</summary>
    public static readonly TimeSpan UnknownMarketSeries = TimeSpan.FromHours(12);

    public static bool IsFresh(OdCached<OdQuote> cached, DateTimeOffset now, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(cached);
        var age = now - cached.FetchedAt;
        if (age < TimeSpan.Zero)
        {
            // The clock went back: the figure's age is unknown.
            return false;
        }

        var ticker = cached.Value.Ticker;
        if (OdMarketHours.IsOpen(ticker, now) is not { } open)
        {
            return age < (cached.Value.IsMarketOpen == true ? interval : UnknownMarketClosed);
        }

        if (open)
        {
            // A weekday the market did not open on (a holiday) needs no refresh until the next session.
            var holiday = cached.Value.IsMarketOpen == false && cached.FetchedAt >= OdMarketHours.SessionOpen(ticker, now);
            return holiday || age < interval;
        }

        var close = OdMarketHours.LastClose(ticker, now)!.Value;
        return cached.FetchedAt >= close + AfterClose || now < close + AfterClose;
    }

    public static bool IsFresh(OdCached<OdSeries> cached, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cached);
        if (now < cached.FetchedAt)
        {
            return false;
        }

        // Fetched within the grace after a close, the last bar may not be final yet: it counts as that session's.
        return OdMarketHours.NextClose(cached.Value.Ticker, cached.FetchedAt - SeriesGrace) is { } close
            ? now < close + SeriesGrace
            : now - cached.FetchedAt < UnknownMarketSeries;
    }
}
