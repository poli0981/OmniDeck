using System.Net;
using OmniDeck.Modules.Stocks;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Stocks;

/// <summary>
/// The Twelve Data client against hand-written answers shaped like the real ones (Twelve Data's terms
/// do not allow its data in the repository): invented symbols and figures, real field names.
/// </summary>
public sealed class OdTwelveDataTests
{
    private const string Key = "test-key-0000";

    private const string Acme = """
        {"symbol":"ACME","name":"Acme Corp","exchange":"NASDAQ","mic_code":"XNAS","currency":"USD","datetime":"2026-11-02",
         "timestamp":1762117200,"last_quote_at":1762117320,"open":"100.5","high":"104.25","low":"99.75","close":"103.10000",
         "volume":"1234567","previous_close":"101.00000","change":"2.10000","percent_change":"2.07921","average_volume":"999",
         "is_market_open":false,"fifty_two_week":{"low":"80.00000","high":"120.00000","low_change":"23.1","range":"80.0 - 120.0"}}
        """;

    private static readonly OdTicker AcmeTicker = new("ACME", "XNAS");
    private static readonly OdTicker FooTicker = new("FOO", "XETR");
    private static readonly OdTicker BarTicker = new("BAR", "XNYS");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ABatch_ReadsEachQuote_AndTheListingsThatFailedOnTheirOwn()
    {
        var http = new OdFakeHttp(_ => Json(HttpStatusCode.OK, $$$"""
            {"ACME":{{{Acme}}},
             "FOO:XETR":{"code":404,"message":"This symbol is available starting with the Grow or Venture plan.","status":"error"}}
            """));

        var batch = await new OdTwelveData(new HttpClient(http), Logger.None).QuotesAsync([AcmeTicker, FooTicker, BarTicker], Key, Token);

        var quote = batch.Quotes["ACME:XNAS"];
        Assert.Equal("Acme Corp", quote.Name);
        Assert.Equal(103.1m, quote.Price);
        Assert.Equal(2.07921m, quote.PercentChange);
        Assert.Equal(1234567, quote.Volume);
        Assert.Equal(80m, quote.YearLow);
        Assert.False(quote.IsMarketOpen);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1762117320), quote.AsOf);
        Assert.Equal(OdStocksError.PlanRequired, batch.Failed["FOO:XETR"]);
        Assert.Equal(OdStocksError.NotFound, batch.Failed["BAR:XNYS"]);

        var request = Assert.Single(http.Requests);
        Assert.Equal("/quote", request.RequestUri!.AbsolutePath);
        Assert.Equal("?symbol=ACME,FOO%3AXETR,BAR", request.RequestUri.Query);
        Assert.Equal("apikey", request.Headers.Authorization?.Scheme);
        Assert.Equal(Key, request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain(Key, request.RequestUri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneQuote_ComesBare_AndMissingFiguresAreNull()
    {
        var http = new OdFakeHttp(_ => Json(HttpStatusCode.OK, """
            {"symbol":"EUR/USD","name":"Euro / US Dollar","exchange":"Forex","currency":"USD","close":"1.08","open":null,"timestamp":1762117200}
            """));
        var pair = new OdTicker("EUR/USD", null);

        var batch = await new OdTwelveData(new HttpClient(http), Logger.None).QuotesAsync([pair], Key, Token);

        var quote = batch.Quotes["EUR/USD"];
        Assert.Equal(1.08m, quote.Price);
        Assert.Null(quote.Open);
        Assert.Null(quote.Volume);
        Assert.Null(quote.YearHigh);
        Assert.Null(quote.IsMarketOpen);
        Assert.Empty(batch.Failed);
    }

    [Fact]
    public async Task OneQuote_ThatNeedsAHigherPlan_IsThatListingsFailure()
    {
        var http = new OdFakeHttp(_ => Json(HttpStatusCode.NotFound, """{"code":404,"message":"Available starting with the Pro or Venture plan","status":"error"}"""));

        var batch = await new OdTwelveData(new HttpClient(http), Logger.None).QuotesAsync([FooTicker], Key, Token);

        Assert.Empty(batch.Quotes);
        Assert.Equal(OdStocksError.PlanRequired, batch.Failed["FOO:XETR"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"code":401,"message":"apikey parameter is incorrect","status":"error"}""", OdStocksError.KeyRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":429,"message":"You have run out of API credits for the current minute.","status":"error"}""", OdStocksError.MinuteLimit)]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":429,"message":"You have run out of API credits for the day.","status":"error"}""", OdStocksError.DayLimit)]
    [InlineData(HttpStatusCode.BadGateway, "<html>bad gateway</html>", OdStocksError.Offline)]
    [InlineData(HttpStatusCode.OK, "not json", OdStocksError.BadResponse)]
    [InlineData(HttpStatusCode.OK, """{"code":401,"message":"demo key","status":"error"}""", OdStocksError.KeyRejected)]
    public async Task WholeRequestFailures_Throw_WithWhatTheyMean(HttpStatusCode status, string body, OdStocksError expected)
    {
        var client = new OdTwelveData(new HttpClient(new OdFakeHttp(_ => Json(status, body))), Logger.None);

        var failure = await Assert.ThrowsAsync<OdStocksException>(() => client.QuotesAsync([AcmeTicker, BarTicker], Key, Token));

        Assert.Equal(expected, failure.Error);
    }

    [Fact]
    public async Task NoKey_AndNoNetwork_FailWithoutAnAnswer()
    {
        var http = new OdFakeHttp(_ => throw new HttpRequestException("No such host is known."));
        var client = new OdTwelveData(new HttpClient(http), Logger.None);

        Assert.Equal(OdStocksError.NoKey, (await Assert.ThrowsAsync<OdStocksException>(() => client.UsageAsync(" ", Token))).Error);
        Assert.Empty(http.Requests);
        Assert.Equal(OdStocksError.Offline, (await Assert.ThrowsAsync<OdStocksException>(() => client.UsageAsync(Key, Token))).Error);
    }

    [Fact]
    public async Task ASeries_ComesOldestFirst_WithoutBarsThatLackAPrice()
    {
        var http = new OdFakeHttp(_ => Json(HttpStatusCode.OK, """
            {"meta":{"symbol":"ACME","interval":"1day","currency":"USD","exchange_timezone":"America/New_York","exchange":"NASDAQ","mic_code":"XNAS","type":"Common Stock"},
             "values":[{"datetime":"2026-11-02","open":"101","high":"104","low":"100","close":"103","volume":"500"},
                       {"datetime":"2026-10-31","open":"99","high":"102","low":"98","close":"101"},
                       {"datetime":"2026-10-30","open":"98","high":"100","close":"99","volume":"300"}],
             "status":"ok"}
            """));

        var series = await new OdTwelveData(new HttpClient(http), Logger.None).SeriesAsync(AcmeTicker, OdSeriesRange.Daily, Key, Token);

        Assert.Equal("USD", series.Currency);
        Assert.Equal(2, series.Bars.Count);
        Assert.Equal(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Unspecified), series.Bars[0].Time);
        Assert.Null(series.Bars[0].Volume);
        Assert.Equal(103, series.Bars[1].Close, 6);
        Assert.Contains("interval=1day&outputsize=252", Assert.Single(http.Requests).RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_LabelsEachListingWithItsPlan_AndUsageReadsTheLimits()
    {
        var http = new OdFakeHttp(request => request.RequestUri!.AbsolutePath.EndsWith("api_usage", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, """{"timestamp":"2026-11-02 10:00:00","current_usage":2,"plan_limit":8,"daily_usage":17,"plan_daily_limit":800,"plan_category":"basic"}""")
            : Json(HttpStatusCode.OK, """
                {"data":[{"symbol":"ACME","instrument_name":"Acme Corp","exchange":"NYSE","mic_code":"XNYS","exchange_timezone":"America/New_York",
                          "instrument_type":"Depositary Receipt","country":"United States","currency":"USD","access":{"global":"Level A","plan":"Basic"}},
                         {"symbol":"ACME","instrument_name":"Acme AG","exchange":"XETR","mic_code":"XETR","country":"Germany","currency":"EUR","access":{"plan":"Grow"}},
                         {"instrument_name":"no symbol"}],
                 "status":"ok"}
                """));
        var client = new OdTwelveData(new HttpClient(http), Logger.None);

        var listings = await client.SearchAsync("acme", Key, Token);
        var usage = await client.UsageAsync(Key, Token);

        Assert.Equal(2, listings.Count);
        Assert.Equal("Basic", listings[0].Plan);
        Assert.Equal(new OdTicker("ACME", "XETR"), listings[1].Ticker);
        Assert.Contains("show_plan=true", http.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal(new OdUsage(2, 8, 17, 800, "basic"), usage);
    }

    [Fact]
    public async Task TheLog_NamesNeitherTheKeyNorASymbol()
    {
        var log = new OdLogCapture();
        var client = new OdTwelveData(new HttpClient(new OdFakeHttp(_ => Json(HttpStatusCode.OK, Acme))), log.Logger);

        await client.QuotesAsync([AcmeTicker], Key, Token);

        Assert.NotEmpty(log.Events);
        Assert.All(log.Events, logged =>
        {
            var text = logged.RenderMessage(System.Globalization.CultureInfo.InvariantCulture);
            Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
            Assert.DoesNotContain("ACME", text, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData("AAPL", "XNAS", "AAPL")]
    [InlineData("SAP", "XETR", "SAP:XETR")]
    [InlineData("EUR/USD", null, "EUR/USD")]
    public void Tickers_AskForUsListingsBySymbolAlone(string symbol, string? mic, string query) =>
        Assert.Equal(query, new OdTicker(symbol, mic).Query);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };
}
