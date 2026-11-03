using System.Globalization;
using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The bars a range shows: the last month (22 trading days), six months (126) or a year of the daily
/// series, or the weekly one for five years; then brought up to the live quote, since the series is
/// fetched once a day and the quote more often.
/// </summary>
public static class OdStockBars
{
    public static IReadOnlyList<OdOhlc> For(OdStockRange range, OdSeries? daily, OdSeries? weekly, OdQuote? quote)
    {
        var bars = range switch
        {
            OdStockRange.OneMonth => daily?.Bars.TakeLast(22),
            OdStockRange.SixMonths => daily?.Bars.TakeLast(126),
            OdStockRange.OneYear => daily?.Bars,
            _ => weekly?.Bars,
        };
        return bars is null ? [] : Patch([.. bars], quote, weekly: range == OdStockRange.FiveYears);
    }

    /// <summary>
    /// Brings the last bar up to <paramref name="quote"/>: the same day (or week) is updated, a later one
    /// is added. A quote without a price or a time changes nothing.
    /// </summary>
    public static IReadOnlyList<OdOhlc> Patch(IReadOnlyList<OdOhlc> bars, OdQuote? quote, bool weekly)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (bars.Count == 0 || quote?.Price is not { } price || quote.AsOf is not { } asOf)
        {
            return bars;
        }

        var last = bars[^1];
        var day = OdMarketHours.DayOf(quote.Ticker, asOf);
        var close = (double)price;
        if (SamePeriod(last.Time.Date, day, weekly))
        {
            var updated = last with
            {
                High = Math.Max(last.High, (double?)quote.High ?? close),
                Low = Math.Min(last.Low, (double?)quote.Low ?? close),
                Close = close,
                Volume = weekly ? last.Volume : quote.Volume ?? last.Volume,
            };
            return [.. bars.Take(bars.Count - 1), updated];
        }

        if (day < last.Time.Date)
        {
            return bars;
        }

        var open = (double?)quote.Open ?? last.Close;
        var added = new OdOhlc(day, open, Math.Max((double?)quote.High ?? close, close), Math.Min((double?)quote.Low ?? close, close), close, quote.Volume);
        return [.. bars, added];
    }

    private static bool SamePeriod(DateTime bar, DateTime day, bool weekly) =>
        weekly
            ? ISOWeek.GetYear(bar) == ISOWeek.GetYear(day) && ISOWeek.GetWeekOfYear(bar) == ISOWeek.GetWeekOfYear(day)
            : bar == day;
}
