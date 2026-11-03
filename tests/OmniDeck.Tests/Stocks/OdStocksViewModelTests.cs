using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Stocks;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.Stocks;

// Texts follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdStocksViewModelTests : IDisposable
{
    // Tuesday 3 November 2026, 15:00 UTC: 10:00 in New York, while the market trades.
    private static readonly DateTimeOffset Trading = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);
    private static readonly OdWatchItem Acme = new("ACME", "XNAS", "Acme", "USD");
    private static readonly OdWatchItem Foo = new("FOO", "XETR", "Foo AG", "EUR");

    private readonly OdTempDirectory _temp = new();
    private readonly OdDatabase _database;
    private readonly FakeTimeProvider _time = new(Trading);
    private readonly OdFakeQuotes _quotes = new();
    private readonly OdMemorySecrets _secrets = new();
    private readonly List<OdStocksSettings> _saved = [];
    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;

    public OdStocksViewModelTests()
    {
        // Figures follow the regional format: these tests read them in US English.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        _temp.Dispose();
    }

    [Fact]
    public void WithoutAKey_TheSetupShows_AndNothingIsFetched() => OdSta.Run(async () =>
    {
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme] });

        await stocks.RefreshAsync(force: true);

        Assert.True(stocks.ShowsSetup);
        Assert.False(stocks.ShowsWatchlist);
        Assert.Empty(_quotes.Calls);
        Assert.Equal("N/A", stocks.Rows[0].Price);
    });

    [Fact]
    public void TestingAKey_SavesIt_TakesThePlansLimits_ThenFetches() => OdSta.Run(async () =>
    {
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme] });
        _quotes.Usage = new OdUsage(1, 55, 120, 5000, "grow");

        stocks.KeyDraft = "  my-key  ";
        await stocks.TestKeyCommand.ExecuteAsync(null);

        Assert.Equal("my-key", _secrets.Read(OdStocksViewModel.SecretName));
        Assert.True(stocks.ShowsWatchlist);
        Assert.Equal("", stocks.KeyDraft);
        Assert.Equal("grow", _saved[^1].Plan);
        Assert.Equal(5000, _saved[^1].DayLimit);
        Assert.Equal(new[] { "usage", "quote:ACME:XNAS", "series:ACME:XNAS" }, _quotes.Calls, StringComparer.Ordinal);
        Assert.Equal("4878 of 5000 Twelve Data credits left today", stocks.Credits);
        Assert.Equal("10.00 USD", stocks.Rows[0].Price);
        Assert.Equal("+0.20 (+2.04%)", stocks.Rows[0].Change);
        Assert.True(stocks.Rows[0].IsUp);
        Assert.Equal(22, stocks.Rows[0].Sparkline.Count);
        Assert.Equal("ACME, ACME Inc, 10.00 USD, up 2.04 percent", stocks.Rows[0].AccessibleName);
    });

    [Theory]
    [InlineData(OdStocksError.KeyRejected, "Twelve Data did not accept this key. Check it and try again.")]
    [InlineData(OdStocksError.Offline, "Twelve Data could not be reached, so the key was not saved.")]
    public void AKeyThatFailsTheTest_IsNotSaved(OdStocksError error, string message) => OdSta.Run(async () =>
    {
        using var stocks = NewViewModel(new OdStocksSettings());
        _quotes.Fails = error;

        stocks.KeyDraft = "wrong";
        await stocks.TestKeyCommand.ExecuteAsync(null);

        Assert.Equal(message, stocks.KeyMessage);
        Assert.Null(_secrets.Read(OdStocksViewModel.SecretName));
        Assert.True(stocks.ShowsSetup);
    });

    [Fact]
    public void Refreshes_FetchOnlyStaleQuotes_AndNeverAgainAListingThePlanDoesNotReach() => OdSta.Run(async () =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        _quotes.Unavailable["FOO:XETR"] = OdStocksError.PlanRequired;
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme, Foo] });

        await stocks.RefreshAsync(force: false);
        Assert.Equal("Not available on your Twelve Data plan", stocks.Rows[1].Note);
        Assert.Contains("Not available on your Twelve Data plan", stocks.Rows[1].AccessibleName, StringComparison.Ordinal);

        _time.Advance(TimeSpan.FromMinutes(5));
        await stocks.RefreshAsync(force: false);
        await stocks.RefreshAsync(force: true);

        Assert.Equal(new[] { "quote:ACME:XNAS,FOO:XETR", "series:ACME:XNAS", "quote:ACME:XNAS" }, _quotes.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void ASearch_LabelsWhatThePlanReaches_AndAddingAndRemovingSaveTheWatchlist() => OdSta.Run(async () =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        _quotes.Listings =
        [
            new OdListing("ACME", "Acme", "NASDAQ", "XNAS", "United States", "USD", "Common Stock", "Basic"),
            new OdListing("ACME", "Acme AG", "XETR", "XETR", "Germany", "EUR", "Common Stock", "Grow"),
        ];
        using var stocks = NewViewModel(new OdStocksSettings { Plan = "basic" });

        stocks.SearchText = "acme";
        await stocks.SearchCommand.ExecuteAsync(null);
        Assert.True(stocks.SearchResults[0].CanAdd);
        Assert.False(stocks.SearchResults[1].CanAdd);
        Assert.Equal("Needs Twelve Data's Grow plan", stocks.SearchResults[1].PlanNote);
        Assert.Equal("Add ACME, Acme", stocks.SearchResults[0].AddName);

        await stocks.AddCommand.ExecuteAsync(stocks.SearchResults[0]);
        Assert.Equal("ACME", Assert.Single(_saved[^1].Watchlist).Symbol);
        Assert.Empty(stocks.SearchResults);
        Assert.Equal("10.00 USD", stocks.Rows[0].Price);

        stocks.RemoveCommand.Execute(stocks.Rows[0]);
        Assert.Empty(_saved[^1].Watchlist);
        Assert.True(stocks.IsEmpty);
        Assert.Null(new OdStocksCache(_database).Quote(new OdTicker("ACME", "XNAS")));
    });

    [Fact]
    public void Rows_MoveUpAndDown_AndTheOrderIsSaved() => OdSta.Run(() =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme, Foo] });

        stocks.MoveDownCommand.Execute(stocks.Rows[0]);
        stocks.MoveDownCommand.Execute(stocks.Rows[1]);
        Assert.Equal(new[] { "FOO", "ACME" }, _saved[^1].Watchlist.Select(item => item.Symbol), StringComparer.Ordinal);

        stocks.MoveUpCommand.Execute(stocks.Rows[1]);
        Assert.Equal(new[] { "ACME", "FOO" }, _saved[^1].Watchlist.Select(item => item.Symbol), StringComparer.Ordinal);
        return Task.CompletedTask;
    });

    [Fact]
    public void SpentCredits_AndAnUnreachableService_SaySo_AndKeepTheLastFigures() => OdSta.Run(async () =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        var limiter = new OdCreditLimiter(new OdAppMeta(_database), _time);
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme] }, limiter);
        await stocks.RefreshAsync(force: true);
        Assert.False(stocks.HasStatus);

        limiter.Spend(8);
        await stocks.RefreshAsync(force: true);
        Assert.Equal(InfoBarSeverity.Informational, stocks.StatusSeverity);
        Assert.StartsWith("This minute's Twelve Data credits are spent.", stocks.Status, StringComparison.Ordinal);

        _time.Advance(TimeSpan.FromMinutes(1));
        _quotes.Fails = OdStocksError.Offline;
        await stocks.RefreshAsync(force: true);
        Assert.Equal(InfoBarSeverity.Warning, stocks.StatusSeverity);
        Assert.Equal("10.00 USD", stocks.Rows[0].Price);

        limiter.Sync(800);
        await stocks.RefreshAsync(force: true);
        Assert.StartsWith("Today's Twelve Data credits are spent.", stocks.Status, StringComparison.Ordinal);
    });

    [Fact]
    public void OffScreen_ItStopsRefreshing() => OdSta.Run(async () =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme], RefreshMinutes = 1 });

        stocks.Activate();
        await Task.Delay(50);
        var calls = _quotes.Calls.Count;
        _time.Advance(TimeSpan.FromMinutes(2));
        await Task.Delay(50);
        Assert.True(_quotes.Calls.Count > calls);

        stocks.Deactivate();
        calls = _quotes.Calls.Count;
        _time.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(50);
        Assert.Equal(calls, _quotes.Calls.Count);
    });

    [Fact]
    public void RemovingTheKey_KeepsTheWatchlist_AndShowsTheSetup() => OdSta.Run(async () =>
    {
        _secrets.Write(OdStocksViewModel.SecretName, "key");
        using var stocks = NewViewModel(new OdStocksSettings { Watchlist = [Acme] });
        await stocks.RefreshAsync(force: true);

        stocks.RemoveKeyCommand.Execute(null);

        Assert.Null(_secrets.Read(OdStocksViewModel.SecretName));
        Assert.True(stocks.ShowsSetup);
        Assert.Single(stocks.Rows);
        Assert.Equal("10.00 USD", stocks.Rows[0].Price);

        stocks.ChangeKeyCommand.Execute(null);
        Assert.True(stocks.IsEditingKey);
        stocks.CancelKeyCommand.Execute(null);
        Assert.False(stocks.IsEditingKey);
    });

    private OdStocksViewModel NewViewModel(OdStocksSettings settings, OdCreditLimiter? limiter = null) => new(
        settings,
        _saved.Add,
        _secrets,
        _quotes,
        new OdStocksCache(_database),
        limiter ?? new OdCreditLimiter(new OdAppMeta(_database), _time),
        _time,
        Logger.None);
}
