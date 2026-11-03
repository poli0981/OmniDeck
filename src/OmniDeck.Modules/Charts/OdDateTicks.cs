using System.Globalization;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// Where a time axis puts its labels: on the first point of each period, the finest period whose
/// labels fit. Hourly series step by 3, 6, 12 or 24 hours and name the day at midnight; daily and
/// weekly ones by day, week, month, a few months or years. Labels follow the culture's own patterns.
/// </summary>
public static class OdDateTicks
{
    private static readonly int[] HourSteps = [3, 6, 12, 24];
    private static readonly int[] MonthSteps = [1, 2, 3, 6];
    private static readonly int[] YearSteps = [1, 2, 5, 10];

    /// <summary>At most <paramref name="maxLabels"/> labels for points at <paramref name="times"/>, in order.</summary>
    public static IReadOnlyList<OdTick> Choose(IReadOnlyList<DateTime> times, int maxLabels, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(culture);
        if (times.Count == 0 || maxLabels <= 0)
        {
            return [];
        }

        var hourly = times.Count > 1 && (times[^1] - times[0]).TotalHours / (times.Count - 1) < 12;
        foreach (var candidate in hourly ? HourlyCandidates(culture) : DailyCandidates(culture))
        {
            var ticks = Pick(times, candidate);
            if (ticks.Count <= maxLabels)
            {
                return ticks;
            }
        }

        return [];
    }

    private static List<OdTick> Pick(IReadOnlyList<DateTime> times, Candidate candidate)
    {
        var ticks = new List<OdTick>();
        for (var i = 1; i < times.Count; i++)
        {
            var key = candidate.Key(times[i]);
            if (key != candidate.Key(times[i - 1]) && key % candidate.Every == 0)
            {
                ticks.Add(new OdTick(i, candidate.Label(times[i])));
            }
        }

        return ticks;
    }

    private static IEnumerable<Candidate> HourlyCandidates(CultureInfo culture)
    {
        foreach (var step in HourSteps)
        {
            yield return new Candidate(
                time => (long)(time - DateTime.MinValue).TotalHours,
                step,
                time => time.Hour == 0 ? time.ToString("ddd", culture) : time.ToString("t", culture));
        }
    }

    private static IEnumerable<Candidate> DailyCandidates(CultureInfo culture)
    {
        var dayPattern = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        yield return new Candidate(time => time.Date.Ticks / TimeSpan.TicksPerDay, 1, time => time.ToString(dayPattern, culture));
        yield return new Candidate(WeekOf, 1, time => time.ToString(dayPattern, culture));
        foreach (var step in MonthSteps)
        {
            yield return new Candidate(
                time => ((time.Year * 12L) + time.Month) - 1,
                step,
                time => time.Month == 1 ? time.ToString("yyyy", culture) : time.ToString("MMM", culture));
        }

        foreach (var step in YearSteps)
        {
            yield return new Candidate(time => time.Year, step, time => time.ToString("yyyy", culture));
        }
    }

    /// <summary>The week a day falls in, counted in weeks that start on Monday.</summary>
    private static long WeekOf(DateTime time)
    {
        var sinceMonday = ((int)time.DayOfWeek + 6) % 7;
        return (time.Date.AddDays(-sinceMonday).Ticks / TimeSpan.TicksPerDay) / 7;
    }

    /// <summary>A period: the key that changes when a new one starts, how many keys apart labels are, and the label.</summary>
    private sealed record Candidate(Func<DateTime, long> Key, int Every, Func<DateTime, string> Label);
}
