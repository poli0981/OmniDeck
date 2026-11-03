using System.Collections.Frozen;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// When the US markets trade: 09:30 to 16:00 New York time, Monday to Friday. Holidays are not listed
/// here: every quote says whether its market is open, which tells a holiday from a trading day. Other
/// markets have no schedule here, so their quotes refresh more cautiously.
/// </summary>
public static class OdMarketHours
{
    private static readonly FrozenSet<string> UnitedStates = FrozenSet.ToFrozenSet(
        ["XNAS", "XNGS", "XNMS", "XNCM", "XNYS", "ARCX", "XASE", "BATS", "BATY", "EDGA", "EDGX", "IEXG", "MEMX", "XCHI", "XBOS", "XPHL", "XCIS"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan OpensAt = new(9, 30, 0);
    private static readonly TimeSpan ClosesAt = new(16, 0, 0);
    private static readonly Lazy<TimeZoneInfo> NewYork = new(() => TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));

    /// <summary>Whether a market is one of the US exchanges; a listing without one is taken as US, the free plan's market.</summary>
    public static bool IsUnitedStates(string? mic) => mic is null || UnitedStates.Contains(mic);

    /// <summary>Whether the schedule has <paramref name="ticker"/>'s market open at <paramref name="now"/>; null for a market without a schedule here.</summary>
    public static bool? IsOpen(OdTicker ticker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (!IsUnitedStates(ticker.Mic))
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTime(now, NewYork.Value);
        return IsTradingDay(local.Date) && local.TimeOfDay >= OpensAt && local.TimeOfDay < ClosesAt;
    }

    /// <summary>The first scheduled close at or after <paramref name="after"/>; null for a market without a schedule here.</summary>
    public static DateTimeOffset? NextClose(OdTicker ticker, DateTimeOffset after)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (!IsUnitedStates(ticker.Mic))
        {
            return null;
        }

        var day = TimeZoneInfo.ConvertTime(after, NewYork.Value).Date;
        while (true)
        {
            if (IsTradingDay(day) && At(day, ClosesAt) >= after)
            {
                return At(day, ClosesAt);
            }

            day = day.AddDays(1);
        }
    }

    /// <summary>The last scheduled close before <paramref name="now"/>; null for a market without a schedule here.</summary>
    public static DateTimeOffset? LastClose(OdTicker ticker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (!IsUnitedStates(ticker.Mic))
        {
            return null;
        }

        var day = TimeZoneInfo.ConvertTime(now, NewYork.Value).Date;
        while (true)
        {
            if (IsTradingDay(day) && At(day, ClosesAt) <= now)
            {
                return At(day, ClosesAt);
            }

            day = day.AddDays(-1);
        }
    }

    /// <summary>The scheduled open of the session <paramref name="now"/> falls in or follows; null for a market without a schedule here.</summary>
    public static DateTimeOffset? SessionOpen(OdTicker ticker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        if (!IsUnitedStates(ticker.Mic))
        {
            return null;
        }

        var day = TimeZoneInfo.ConvertTime(now, NewYork.Value).Date;
        while (!IsTradingDay(day) || At(day, ClosesAt) <= now)
        {
            day = day.AddDays(1);
        }

        return At(day, OpensAt);
    }

    private static bool IsTradingDay(DateTime day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static DateTimeOffset At(DateTime day, TimeSpan time)
    {
        var local = DateTime.SpecifyKind(day + time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, NewYork.Value.GetUtcOffset(local));
    }
}
