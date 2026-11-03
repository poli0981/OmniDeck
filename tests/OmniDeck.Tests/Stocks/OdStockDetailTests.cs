using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Charts;
using OmniDeck.Modules.Stocks;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Stocks;

// Texts follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdStockDetailTests : IDisposable
{
    private static readonly OdTicker Acme = new("ACME", "XNAS");
    private static readonly OdWatchItem AcmeItem = new("ACME", "XNAS", "Acme", "USD");
    private static readonly DateTime Monday = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly OdTempDirectory _temp = new();

    public OdStockDetailTests()
    {
        // Figures follow the regional format: these tests read them in US English.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        _temp.Dispose();
    }

    [Fact]
    public void Ranges_TakeTheirShareOfTheDailyBars_OrTheWeeklyOnes()
    {
        var daily = Series(OdSeriesRange.Daily, 252);
        var weekly = Series(OdSeriesRange.Weekly, 261);

        Assert.Equal(22, OdStockBars.For(OdStockRange.OneMonth, daily, weekly, null).Count);
        Assert.Equal(126, OdStockBars.For(OdStockRange.SixMonths, daily, weekly, null).Count);
        Assert.Equal(252, OdStockBars.For(OdStockRange.OneYear, daily, weekly, null).Count);
        Assert.Equal(261, OdStockBars.For(OdStockRange.FiveYears, daily, weekly, null).Count);
        Assert.Empty(OdStockBars.For(OdStockRange.FiveYears, daily, null, null));
        Assert.Equal(daily.Bars[^22], OdStockBars.For(OdStockRange.OneMonth, daily, weekly, null)[0]);
    }

    [Fact]
    public void TheLastBar_FollowsTheLiveQuote()
    {
        IReadOnlyList<OdOhlc> bars = [new(Monday.AddDays(-1), 10, 11, 9, 10.5, 100), new(Monday, 10.5, 11, 10, 10.8, 200)];

        // 16:00 in New York on Monday is 21:00 UTC: the same trading day, even past midnight UTC.
        var sameDay = OdStockBars.Patch(bars, Quote(12m, new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero)), weekly: false);
        Assert.Equal(2, sameDay.Count);
        Assert.Equal(12, sameDay[^1].Close, 6);
        Assert.Equal(12, sameDay[^1].High, 6);
        Assert.Equal(10, sameDay[^1].Low, 6);

        var nextDay = OdStockBars.Patch(bars, Quote(9m, new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero)), weekly: false);
        Assert.Equal(3, nextDay.Count);
        Assert.Equal(Monday.AddDays(1), nextDay[^1].Time);
        Assert.Equal(9, nextDay[^1].Low, 6);

        Assert.Same(bars, OdStockBars.Patch(bars, Quote(9m, new DateTimeOffset(2026, 10, 30, 15, 0, 0, TimeSpan.Zero)), weekly: false));
        Assert.Same(bars, OdStockBars.Patch(bars, Quote(null, DateTimeOffset.UnixEpoch), weekly: false));
        Assert.Equal(2, OdStockBars.Patch(bars, Quote(12m, new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero)), weekly: true).Count);
    }

    [Fact]
    public void TheDetail_SumsTheRangeUp_ReadsTheBarUnderTheCursor_AndTabulatesIt() => OdSta.Run(async () =>
    {
        var loads = new List<OdSeriesRange>();
        var daily = Series(OdSeriesRange.Daily, 252);
        var detail = new OdStockDetailViewModel(AcmeItem, null, range =>
        {
            loads.Add(range);
            return Task.FromResult<OdSeries?>(range == OdSeriesRange.Daily ? daily : Series(OdSeriesRange.Weekly, 261));
        });

        await detail.LoadAsync();
        Assert.Equal("6 months", detail.SelectedRange?.Label);
        Assert.Equal(126, detail.Bars.Count);
        Assert.StartsWith("ACME, 6 months: from ", detail.Summary, StringComparison.Ordinal);
        Assert.Equal(detail.Bars[^1].Time.ToString("d", CultureInfo.CurrentCulture), detail.Table[0].Date);

        detail.Cursor = 0;
        Assert.StartsWith(detail.Bars[0].Time.ToString("d", CultureInfo.CurrentCulture) + ": open ", detail.Readout, StringComparison.Ordinal);
        Assert.Equal(detail.Readout, detail.Table[^1].ToString());

        detail.SelectedRange = detail.Ranges[3];
        await Task.Delay(50);
        detail.SelectedRange = detail.Ranges[0];
        detail.SelectedRange = detail.Ranges[3];
        await Task.Delay(50);
        Assert.Equal(261, detail.Bars.Count);
        Assert.Equal(-1, detail.Cursor);
        Assert.Equal(new[] { OdSeriesRange.Daily, OdSeriesRange.Weekly }, loads);
        Assert.False(detail.HasMessage);
    });

    [Fact]
    public void ADetailWithoutBars_SaysSo() => OdSta.Run(async () =>
    {
        var detail = new OdStockDetailViewModel(AcmeItem, null, _ => Task.FromResult<OdSeries?>(null));

        await detail.LoadAsync();

        Assert.Empty(detail.Bars);
        Assert.Equal("", detail.Summary);
        Assert.Equal("These prices are not available right now.", detail.Message);
        Assert.Equal("Today: N/A to N/A", detail.DayRange);
    });

    [Fact]
    public void ChoosingARow_OpensItsDetail_FromTheCacheWhileItIsFresh() => OdSta.Run(async () =>
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 15, 0, 0, TimeSpan.Zero));
        var secrets = new OdMemorySecrets();
        secrets.Write(OdStocksViewModel.SecretName, "key");
        var quotes = new OdFakeQuotes();
        var cache = new OdStocksCache(database);
        using var stocks = new OdStocksViewModel(new OdStocksSettings { Watchlist = [AcmeItem] }, _ => { }, secrets, quotes, cache, new OdCreditLimiter(new OdAppMeta(database), time), time, Logger.None);
        await stocks.RefreshAsync(force: true);
        var calls = quotes.Calls.Count;

        stocks.SelectedRow = stocks.Rows[0];
        await Task.Delay(50);
        Assert.True(stocks.HasDetail);
        Assert.Equal(30, stocks.Detail!.Bars.Count);
        Assert.Equal(calls, quotes.Calls.Count);

        Assert.NotNull(await stocks.LoadSeriesAsync(Acme, OdSeriesRange.Weekly));
        Assert.Equal("series:ACME:XNAS", quotes.Calls[^1]);
        Assert.NotNull(await stocks.LoadSeriesAsync(Acme, OdSeriesRange.Weekly));
        Assert.Equal(calls + 1, quotes.Calls.Count);

        stocks.CloseDetailCommand.Execute(null);
        Assert.False(stocks.HasDetail);
    });

    private static OdSeries Series(OdSeriesRange range, int count) => new(
        Acme,
        range,
        "USD",
        [.. Enumerable.Range(0, count).Select(i => new OdOhlc(Monday.AddDays((i - count + 1) * (range == OdSeriesRange.Weekly ? 7 : 1)), 10 + (i % 5), 12 + (i % 5), 9 + (i % 5), 11 + (i % 5), 1000 + i))]);

    private static OdQuote Quote(decimal? price, DateTimeOffset asOf) =>
        new(Acme, "Acme", "NASDAQ", "USD", price, 10m, price, null, null, null, null, 300, null, null, true, asOf);
}
