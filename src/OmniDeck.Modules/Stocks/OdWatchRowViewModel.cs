using CommunityToolkit.Mvvm.ComponentModel;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// One row of the watchlist: the price, the day's change with its direction, a sparkline of the last
/// month and when the price is from, or why there is none. Screen readers hear it as one sentence.
/// </summary>
public sealed partial class OdWatchRowViewModel(OdWatchItem item) : ObservableObject
{
    public OdWatchItem Item { get; } = item;

    public string Symbol => Item.Symbol;

    [ObservableProperty]
    private string _name = item.Name;

    [ObservableProperty]
    private string _price = OdStocksText.NotAvailable;

    [ObservableProperty]
    private string _change = "";

    /// <summary>True when the price rose today, false when it fell, null without a figure.</summary>
    [ObservableProperty]
    private bool? _isUp;

    [ObservableProperty]
    private IReadOnlyList<double> _sparkline = [];

    [ObservableProperty]
    private string _asOf = "";

    /// <summary>Why the row has no price, or that it is old; empty otherwise.</summary>
    [ObservableProperty]
    private string _note = "";

    [ObservableProperty]
    private string _accessibleName = item.Symbol;

    /// <summary>Shows <paramref name="quote"/>, the last month of <paramref name="series"/>, and <paramref name="note"/>.</summary>
    public void Show(OdQuote? quote, OdSeries? series, string note, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (quote?.Name is { Length: > 0 } name)
        {
            Name = name;
        }

        Price = OdStocksText.Price(quote?.Price, quote?.Currency ?? Item.Currency);
        Change = OdStocksText.Change(quote?.Change, quote?.PercentChange);
        IsUp = quote?.Change is { } change ? change >= 0 : null;
        Sparkline = series is null ? [] : [.. series.Bars.TakeLast(22).Select(bar => bar.Close)];
        AsOf = OdStocksText.AsOf(quote?.AsOf, time);
        Note = note;
        AccessibleName = OdStocksText.Row(Symbol, Name, Price, quote?.PercentChange, note);
    }

    public override string ToString() => AccessibleName;
}
