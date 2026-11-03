using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Charts;
using OmniDeck.Modules.Stocks;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Stocks;

public sealed class OdStocksDataTests : IDisposable
{
    // Tuesday 3 November 2026, 14:00 UTC: 09:00 in New York (EST), half an hour before the open.
    private static readonly DateTimeOffset BeforeOpen = new(2026, 11, 3, 14, 0, 0, TimeSpan.Zero);
    private static readonly OdTicker Acme = new("ACME", "XNAS");
    private static readonly OdTicker Abroad = new("ACME", "XETR");

    private readonly OdTempDirectory _temp = new();
    private readonly OdDatabase _database;
    private readonly FakeTimeProvider _time = new(BeforeOpen);

    public OdStocksDataTests()
    {
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheLimiter_KeepsToEightCreditsInAnyMinute()
    {
        var limiter = new OdCreditLimiter(new OdAppMeta(_database), _time);

        limiter.Spend(5);
        _time.Advance(TimeSpan.FromSeconds(20));
        limiter.Spend(3);

        Assert.Equal(TimeSpan.FromSeconds(40), limiter.WaitFor(1));
        Assert.Equal(TimeSpan.FromSeconds(40), limiter.WaitFor(5));
        Assert.Equal(TimeSpan.FromSeconds(60), limiter.WaitFor(6));
        _time.Advance(TimeSpan.FromSeconds(40));
        Assert.Equal(TimeSpan.Zero, limiter.WaitFor(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => limiter.WaitFor(9));
        Assert.Equal(8, limiter.UsedToday);
    }

    [Fact]
    public void TheLimiter_KeepsTheDaysCount_AcrossRestarts_UntilMidnightUtc()
    {
        var meta = new OdAppMeta(_database);
        var limiter = new OdCreditLimiter(meta, _time);
        limiter.Sync(797);
        limiter.Spend(2);

        var restarted = new OdCreditLimiter(meta, _time);
        Assert.Equal(799, restarted.UsedToday);
        Assert.Equal(1, restarted.LeftToday);
        Assert.Equal(TimeSpan.Zero, restarted.WaitFor(1));
        Assert.Null(restarted.WaitFor(2));

        _time.SetUtcNow(restarted.NextDay);
        Assert.Equal(0, restarted.UsedToday);
        Assert.Equal(TimeSpan.Zero, restarted.WaitFor(8));
    }

    [Fact]
    public void TheLimiter_StopsAfterA429_UntilTheNextMinuteOrDay()
    {
        var limiter = new OdCreditLimiter(new OdAppMeta(_database), _time);
        _time.Advance(TimeSpan.FromSeconds(15));

        limiter.Block(day: false);
        Assert.Equal(TimeSpan.FromSeconds(45), limiter.WaitFor(1));

        limiter.Block(day: true);
        Assert.Null(limiter.WaitFor(1));
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero), limiter.NextDay);

        limiter.SetLimits(55, 5000);
        Assert.Equal(55, limiter.MinuteLimit);
        Assert.Equal(5000, limiter.DayLimit);
    }

    [Fact]
    public void TheLimiter_StartsAgain_FromADamagedCount()
    {
        var meta = new OdAppMeta(_database);
        meta.Set("stocks.credits", "{not json");

        Assert.Equal(0, new OdCreditLimiter(meta, _time).UsedToday);
    }

    [Fact]
    public void TheUsSchedule_KnowsTheSession()
    {
        var open = BeforeOpen.AddMinutes(30);
        var close = BeforeOpen.AddHours(7);

        Assert.False(OdMarketHours.IsOpen(Acme, BeforeOpen));
        Assert.True(OdMarketHours.IsOpen(Acme, open));
        Assert.False(OdMarketHours.IsOpen(Acme, close));
        Assert.Null(OdMarketHours.IsOpen(Abroad, open));
        Assert.Equal(close, OdMarketHours.NextClose(Acme, open));
        Assert.Equal(close, OdMarketHours.NextClose(Acme, BeforeOpen));
        Assert.Equal(close.AddDays(-1), OdMarketHours.LastClose(Acme, open));
        Assert.Equal(open, OdMarketHours.SessionOpen(Acme, BeforeOpen));
        Assert.Equal(open.AddDays(1), OdMarketHours.SessionOpen(Acme, close));

        // Saturday 7 November: the next session is Monday's.
        var saturday = new DateTimeOffset(2026, 11, 7, 15, 0, 0, TimeSpan.Zero);
        Assert.False(OdMarketHours.IsOpen(Acme, saturday));
        Assert.Equal(close.AddDays(3), OdMarketHours.LastClose(Acme, saturday));
        Assert.Equal(open.AddDays(6), OdMarketHours.SessionOpen(Acme, saturday));
    }

    [Fact]
    public void Quotes_StayFresh_AsLongAsTheirFiguresCannotHaveMoved()
    {
        var interval = TimeSpan.FromMinutes(15);
        var open = BeforeOpen.AddMinutes(30);
        var close = BeforeOpen.AddHours(7);

        // Trading: the refresh interval.
        Assert.True(OdStocksFreshness.IsFresh(Cached(Quote(Acme, true), open), open.AddMinutes(14), interval));
        Assert.False(OdStocksFreshness.IsFresh(Cached(Quote(Acme, true), open), open.AddMinutes(15), interval));

        // After the close: fresh until the final price is due, then once more after it.
        var lastTrade = Cached(Quote(Acme, true), close.AddMinutes(-5));
        Assert.True(OdStocksFreshness.IsFresh(lastTrade, close.AddMinutes(10), interval));
        Assert.False(OdStocksFreshness.IsFresh(lastTrade, close.AddMinutes(25), interval));
        Assert.True(OdStocksFreshness.IsFresh(Cached(Quote(Acme, false), close.AddMinutes(25)), close.AddHours(16), interval));
        Assert.False(OdStocksFreshness.IsFresh(Cached(Quote(Acme, false), close.AddMinutes(25)), close.AddDays(1).AddMinutes(25), interval));

        // Friday's final price lasts the weekend.
        var friday = close.AddDays(3);
        Assert.True(OdStocksFreshness.IsFresh(Cached(Quote(Acme, false), friday.AddMinutes(25)), friday.AddDays(2), interval));

        // A weekday the market stayed closed on.
        Assert.True(OdStocksFreshness.IsFresh(Cached(Quote(Acme, false), open.AddMinutes(1)), open.AddHours(3), interval));

        // A market without a schedule here, and a clock that went back.
        Assert.True(OdStocksFreshness.IsFresh(Cached(Quote(Abroad, false), open), open.AddMinutes(59), interval));
        Assert.False(OdStocksFreshness.IsFresh(Cached(Quote(Abroad, true), open), open.AddMinutes(15), interval));
        Assert.False(OdStocksFreshness.IsFresh(Cached(Quote(Acme, true), open), open.AddMinutes(-1), interval));
    }

    [Fact]
    public void Series_LastUntilHalfAnHourAfterTheNextClose()
    {
        var close = BeforeOpen.AddHours(7);
        var series = new OdSeries(Acme, OdSeriesRange.Daily, "USD", []);

        Assert.True(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series, BeforeOpen.AddHours(2)), close.AddMinutes(29)));
        Assert.False(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series, BeforeOpen.AddHours(2)), close.AddMinutes(31)));
        Assert.False(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series, close.AddMinutes(10)), close.AddMinutes(31)));
        Assert.True(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series, close.AddMinutes(40)), close.AddDays(1)));
        Assert.True(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series with { Ticker = Abroad }, close), close.AddHours(11)));
        Assert.False(OdStocksFreshness.IsFresh(new OdCached<OdSeries>(series with { Ticker = Abroad }, close), close.AddHours(13)));
    }

    [Fact]
    public void TheCache_KeepsQuotesAndSeries_UntilTheListingIsForgotten()
    {
        var cache = new OdStocksCache(_database);
        var quote = Quote(Acme, true);
        var bars = new[] { new OdOhlc(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Unspecified), 1, 2, 0.5, 1.5, 100) };
        var series = new OdSeries(Acme, OdSeriesRange.Daily, "USD", bars);

        Assert.Null(cache.Quote(Acme));
        cache.PutQuote(quote, BeforeOpen);
        cache.PutQuote(quote with { Price = 2m }, BeforeOpen.AddMinutes(1));
        cache.PutSeries(series, BeforeOpen);

        Assert.Equal(2m, cache.Quote(Acme)?.Value.Price);
        Assert.Equal(BeforeOpen.AddMinutes(1), cache.Quote(Acme)?.FetchedAt);
        Assert.Equal(1.5, Assert.Single(cache.Series(Acme, OdSeriesRange.Daily)!.Value.Bars).Close, 6);
        Assert.Null(cache.Series(Acme, OdSeriesRange.Weekly));
        Assert.Null(cache.Quote(Abroad));

        cache.Forget(Acme);
        Assert.Null(cache.Quote(Acme));
        Assert.Null(cache.Series(Acme, OdSeriesRange.Daily));
    }

    private static OdQuote Quote(OdTicker ticker, bool? open) =>
        new(ticker, "Acme Corp", "NASDAQ", "USD", 1m, null, null, null, null, null, null, null, null, null, open, null);

    private static OdCached<OdQuote> Cached(OdQuote quote, DateTimeOffset fetchedAt) => new(quote, fetchedAt);
}
