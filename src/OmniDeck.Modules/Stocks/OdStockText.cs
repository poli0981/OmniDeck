using System.Globalization;
using OmniDeck.Core.Localization;
using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Stocks;

/// <summary>A price chart in words: its summary, the bar under the cursor, and the table's rows.</summary>
internal static class OdStockText
{
    /// <summary>"ACME, 6 months: from 98.00 to 103.00 USD (+5.10%); high … on …, low … on …"; empty without bars.</summary>
    public static string Summary(string symbol, string range, IReadOnlyList<OdOhlc> bars, string? currency)
    {
        if (bars.Count == 0)
        {
            return "";
        }

        var first = bars[0].Close;
        var last = bars[^1].Close;
        var high = bars.MaxBy(bar => bar.High)!;
        var low = bars.MinBy(bar => bar.Low)!;
        var change = first > 0 ? ((last - first) / first).ToString("+0.00%;-0.00%;0.00%", CultureInfo.CurrentCulture) : OdStocksText.NotAvailable;
        return OdLocalization.Format("Stocks_DetailSummary",
            symbol,
            range,
            Price(first, null),
            Price(last, currency),
            change,
            Price(high.High, currency),
            Day(high.Time),
            Price(low.Low, currency),
            Day(low.Time));
    }

    public static string Readout(OdOhlc bar, string? currency) => OdLocalization.Format("Stocks_DetailReadout",
        Day(bar.Time),
        Price(bar.Open, currency),
        Price(bar.High, currency),
        Price(bar.Low, currency),
        Price(bar.Close, currency),
        Volume(bar.Volume));

    public static OdBarRow Row(OdOhlc bar, string? currency) =>
        new(Day(bar.Time), Price(bar.Open, currency), Price(bar.High, currency), Price(bar.Low, currency), Price(bar.Close, currency), Volume(bar.Volume), Readout(bar, currency));

    private static string Price(double value, string? currency) => OdStocksText.Price((decimal)value, currency);

    private static string Day(DateTime time) => time.ToString("d", CultureInfo.CurrentCulture);

    private static string Volume(double? volume) => volume is { } value ? value.ToString("N0", CultureInfo.CurrentCulture) : OdStocksText.NotAvailable;
}
