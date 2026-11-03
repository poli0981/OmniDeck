using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;
using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// A listing in detail: the day's and the year's range, the volume, whether its market trades, and a
/// price chart over one month to five years. The chart reads out the bar under the cursor, and the
/// same bars can show as a table. The year of daily bars is usually cached already; the five years of
/// weekly bars are fetched, one credit, only when that range is chosen.
/// </summary>
public sealed partial class OdStockDetailViewModel : ObservableObject
{
    private readonly OdQuote? _quote;
    private readonly Func<OdSeriesRange, Task<OdSeries?>> _load;
    private OdSeries? _daily;
    private OdSeries? _weekly;
    private bool _loaded;

    public OdStockDetailViewModel(OdWatchItem item, OdQuote? quote, Func<OdSeriesRange, Task<OdSeries?>> load)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        _quote = quote;
        _load = load;
        Ranges =
        [
            new(OdStockRange.OneMonth, OdLocalization.Get("Stocks_Range1M")),
            new(OdStockRange.SixMonths, OdLocalization.Get("Stocks_Range6M")),
            new(OdStockRange.OneYear, OdLocalization.Get("Stocks_Range1Y")),
            new(OdStockRange.FiveYears, OdLocalization.Get("Stocks_Range5Y")),
        ];
        SelectedRange = Ranges[1];
        Title = OdLocalization.Format("Stocks_ResultTitle", item.Symbol, quote?.Name ?? item.Name);
        var currency = quote?.Currency ?? item.Currency;
        Exchange = OdLocalization.Format("Stocks_DetailMarket", quote?.Exchange ?? item.Mic ?? "", quote?.IsMarketOpen == true ? OdLocalization.Get("Stocks_MarketOpen") : OdLocalization.Get("Stocks_MarketClosed"));
        DayRange = OdLocalization.Format("Stocks_DayRange", OdStocksText.Price(quote?.Low, currency), OdStocksText.Price(quote?.High, currency));
        YearRange = OdLocalization.Format("Stocks_YearRange", OdStocksText.Price(quote?.YearLow, currency), OdStocksText.Price(quote?.YearHigh, currency));
        Volume = OdLocalization.Format("Stocks_Volume", quote?.Volume is { } volume ? volume.ToString("N0", CultureInfo.CurrentCulture) : OdStocksText.NotAvailable);
    }

    public OdWatchItem Item { get; }

    public string Title { get; }

    public string Exchange { get; }

    public string DayRange { get; }

    public string YearRange { get; }

    public string Volume { get; }

    public IReadOnlyList<OdStockRangeOption> Ranges { get; }

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    private OdStockRangeOption? _selectedRange;

    [ObservableProperty]
    private IReadOnlyList<OdOhlc> _bars = [];

    [ObservableProperty]
    private IReadOnlyList<OdBarRow> _table = [];

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private int _cursor = -1;

    [ObservableProperty]
    private string _readout = "";

    [ObservableProperty]
    private bool _showTable;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Why the range has no bars to show; null when it has.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    /// <summary>Loads the daily bars, which most ranges need, and shows the chosen range.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _daily = await _load(OdSeriesRange.Daily);
        }
        finally
        {
            IsLoading = false;
        }

        _loaded = true;
        await ShowAsync();
    }

    partial void OnSelectedRangeChanged(OdStockRangeOption? value)
    {
        if (_loaded)
        {
            _ = ShowAsync();
        }
    }

    partial void OnCursorChanged(int value) =>
        Readout = value >= 0 && value < Bars.Count ? OdStockText.Readout(Bars[value], Item.Currency) : "";

    private async Task ShowAsync()
    {
        var range = SelectedRange?.Range ?? OdStockRange.SixMonths;
        if (range == OdStockRange.FiveYears && _weekly is null)
        {
            IsLoading = true;
            try
            {
                _weekly = await _load(OdSeriesRange.Weekly);
            }
            finally
            {
                IsLoading = false;
            }
        }

        if (range != (SelectedRange?.Range ?? OdStockRange.SixMonths))
        {
            // Another range was chosen while this one loaded.
            return;
        }

        Cursor = -1;
        Bars = OdStockBars.For(range, _daily, _weekly, _quote);
        Table = [.. Bars.Reverse().Select(bar => OdStockText.Row(bar, Item.Currency))];
        Summary = OdStockText.Summary(Item.Symbol, SelectedRange?.Label ?? "", Bars, Item.Currency);
        Message = Bars.Count == 0 ? OdLocalization.Get("Stocks_SeriesUnavailable") : null;
    }
}
