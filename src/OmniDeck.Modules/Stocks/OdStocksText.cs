using System.Globalization;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Stocks;

/// <summary>Market figures as the screen shows them: numbers in the regional format, words in the UI language.</summary>
internal static class OdStocksText
{
    public static string NotAvailable => OdLocalization.Get("Stocks_NotAvailable");

    public static string Price(decimal? price, string? currency) => price is { } value
        ? OdLocalization.Format("Stocks_Price", value.ToString("N2", CultureInfo.CurrentCulture), currency ?? "").Trim()
        : NotAvailable;

    /// <summary>The day's change with its sign, and the same in percent: "+2.10 (+2.08%)".</summary>
    public static string Change(decimal? change, decimal? percent) => change is { } value && percent is { } share
        ? OdLocalization.Format("Stocks_Change", value.ToString("+0.00;-0.00;0.00", CultureInfo.CurrentCulture), (share / 100).ToString("+0.00%;-0.00%;0.00%", CultureInfo.CurrentCulture))
        : "";

    /// <summary>When a price is from: the time today, the date and time otherwise; empty without one.</summary>
    public static string AsOf(DateTimeOffset? asOf, TimeProvider? time = null)
    {
        if (asOf is not { } stamp)
        {
            return "";
        }

        var clock = time ?? TimeProvider.System;
        var local = TimeZoneInfo.ConvertTime(stamp, clock.LocalTimeZone);
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone).Date;
        return local.ToString(local.Date == today ? "t" : "g", CultureInfo.CurrentCulture);
    }

    /// <summary>A watchlist row as one sentence for screen readers.</summary>
    public static string Row(string symbol, string name, string price, decimal? percent, string note)
    {
        var sentence = percent switch
        {
            null => OdLocalization.Format("Stocks_RowPrice", symbol, name, price),
            >= 0 => OdLocalization.Format("Stocks_RowUp", symbol, name, price, percent.Value.ToString("N2", CultureInfo.CurrentCulture)),
            _ => OdLocalization.Format("Stocks_RowDown", symbol, name, price, Math.Abs(percent.Value).ToString("N2", CultureInfo.CurrentCulture)),
        };
        return note.Length == 0 ? sentence : OdLocalization.Format("Stocks_RowNote", sentence, note);
    }
}
