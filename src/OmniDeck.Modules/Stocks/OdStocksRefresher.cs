using Serilog;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// Spends the key's credits on what can have changed (docs/SPEC.md §3.5): the stale quotes first, in
/// batches of a minute's credits, then the daily series behind the sparklines, but only while more
/// than <see cref="Reserve"/> credits are left today, so what the user asks for always has some. It
/// never retries on its own: a minute limit says when the rest can go, and the next refresh takes it.
/// </summary>
public sealed class OdStocksRefresher(IOdQuoteProvider provider, OdStocksCache cache, OdCreditLimiter limiter, TimeProvider time, ILogger log)
{
    /// <summary>The credits kept back each day for what the user asks for: a chart, a new listing, Refresh.</summary>
    public const int Reserve = 100;

    private static readonly IReadOnlyDictionary<string, OdStocksError> None = new Dictionary<string, OdStocksError>(StringComparer.Ordinal);

    private readonly ILogger _log = log.ForContext<OdStocksRefresher>();

    /// <summary>
    /// Refreshes the quotes of <paramref name="tickers"/> that are stale (every one, when
    /// <paramref name="force"/>), then their sparklines' series.
    /// </summary>
    public async Task<OdRefreshResult> RefreshAsync(IReadOnlyList<OdTicker> tickers, string key, bool force, TimeSpan interval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tickers);
        var failed = new Dictionary<string, OdStocksError>(StringComparer.Ordinal);
        var now = time.GetUtcNow();
        var stale = tickers.Where(ticker => force || cache.Quote(ticker) is not { } cached || !OdStocksFreshness.IsFresh(cached, now, interval)).ToList();
        foreach (var batch in stale.Chunk(limiter.MinuteLimit))
        {
            if (await QuotesAsync(batch, key, failed, cancellationToken) is { } stopped)
            {
                return stopped with { Failed = failed };
            }
        }

        // A listing that just failed on its own would only fail again for its series.
        var reachable = tickers.Where(ticker => !failed.ContainsKey(ticker.Key)).ToList();
        return await SparklinesAsync(reachable, key, cancellationToken) is { } paused
            ? paused with { Failed = failed }
            : new OdRefreshResult(null, failed, null, DayExhausted: false);
    }

    /// <summary>
    /// The refresh interval that lets today's credits last the rest of the session: the chosen one when
    /// they do, longer when they would not, keeping the reserve and the final price after the close.
    /// </summary>
    public static TimeSpan IntervalFor(int listings, TimeSpan chosen, OdTicker sample, DateTimeOffset now, int leftToday)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (listings == 0 || OdMarketHours.IsOpen(sample, now) != true)
        {
            return chosen;
        }

        var session = OdMarketHours.NextClose(sample, now)!.Value - now;
        var budget = leftToday - Reserve - listings;
        if (budget < listings)
        {
            return session;
        }

        var needed = TimeSpan.FromMinutes(Math.Ceiling(session.TotalMinutes / (budget / listings)));
        return needed > chosen ? needed : chosen;
    }

    private async Task<OdRefreshResult?> QuotesAsync(OdTicker[] batch, string key, Dictionary<string, OdStocksError> failed, CancellationToken cancellationToken)
    {
        if (Gate(batch.Length) is { } waiting)
        {
            return waiting;
        }

        limiter.Spend(batch.Length);
        OdQuoteBatch result;
        try
        {
            result = await provider.QuotesAsync(batch, key, cancellationToken);
        }
        catch (OdStocksException ex)
        {
            return Stopped(ex.Error);
        }

        var fetchedAt = time.GetUtcNow();
        foreach (var quote in result.Quotes.Values)
        {
            cache.PutQuote(quote, fetchedAt);
        }

        foreach (var (ticker, error) in result.Failed)
        {
            failed[ticker] = error;
        }

        return null;
    }

    private async Task<OdRefreshResult?> SparklinesAsync(IReadOnlyList<OdTicker> tickers, string key, CancellationToken cancellationToken)
    {
        foreach (var ticker in tickers)
        {
            if (cache.Series(ticker, OdSeriesRange.Daily) is { } cached && OdStocksFreshness.IsFresh(cached, time.GetUtcNow()))
            {
                continue;
            }

            if (limiter.LeftToday <= Reserve)
            {
                return null;
            }

            if (Gate(1) is { } waiting)
            {
                return waiting;
            }

            limiter.Spend(1);
            try
            {
                cache.PutSeries(await provider.SeriesAsync(ticker, OdSeriesRange.Daily, key, cancellationToken), time.GetUtcNow());
            }
            catch (OdStocksException ex) when (ex.Error is OdStocksError.PlanRequired or OdStocksError.NotFound)
            {
                _log.Debug(ex, "A sparkline's series is not available: {Error}", ex.Error);
            }
            catch (OdStocksException ex)
            {
                return Stopped(ex.Error);
            }
        }

        return null;
    }

    /// <summary>Null when <paramref name="credits"/> may go now; otherwise why not, and until when.</summary>
    private OdRefreshResult? Gate(int credits) => limiter.WaitFor(credits) switch
    {
        null => new OdRefreshResult(null, None, null, DayExhausted: true),
        { } wait when wait > TimeSpan.Zero => new OdRefreshResult(null, None, time.GetUtcNow() + wait, DayExhausted: false),
        _ => null,
    };

    private OdRefreshResult Stopped(OdStocksError error)
    {
        switch (error)
        {
            case OdStocksError.MinuteLimit:
                limiter.Block(day: false);
                return Gate(1) ?? new OdRefreshResult(null, None, null, DayExhausted: false);
            case OdStocksError.DayLimit:
                limiter.Block(day: true);
                return new OdRefreshResult(null, None, null, DayExhausted: true);
            default:
                return new OdRefreshResult(error, None, null, DayExhausted: false);
        }
    }
}
