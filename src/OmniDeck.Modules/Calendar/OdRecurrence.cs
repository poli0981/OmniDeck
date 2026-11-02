using System.Globalization;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// Repeating events. Ical.Net expands the rules, imported ones included (BYDAY, COUNT, …); the editor
/// writes only the simple ones its choices can describe.
/// </summary>
internal static class OdRecurrence
{
    /// <summary>The occurrences of <paramref name="value"/> that overlap [<paramref name="from"/>, <paramref name="to"/>), by start.</summary>
    public static IEnumerable<OdOccurrence> Between(OdCalendarEvent value, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Expand(value, from, to);
    }

    private static IEnumerable<OdOccurrence> Expand(OdCalendarEvent value, DateTime from, DateTime to)
    {
        var length = value.End - value.Start;
        if (value.Recurrence is null)
        {
            if (value.Start < to && value.End > from)
            {
                yield return new OdOccurrence(value, value.Start, value.End);
            }

            yield break;
        }

        var series = new CalendarEvent
        {
            DtStart = At(value.Start, value.AllDay),
            Duration = Duration.FromTimeSpanExact(length),
            RecurrenceRule = new RecurrencePattern(value.Recurrence),
        };
        series.ExceptionDates.AddRange(value.Exceptions.Select(exception => At(exception, value.AllDay)));

        // Starting one event length early also finds the occurrences that began before the window and still run.
        foreach (var occurrence in series.GetOccurrences(At(from - length, value.AllDay), null))
        {
            var start = occurrence.Period.StartTime.Value;
            if (start >= to)
            {
                yield break;
            }

            if (start + length > from)
            {
                yield return new OdOccurrence(value, start, start + length);
            }
        }
    }

    /// <summary>The RRULE for a simple repeat; null for <see cref="OdRepeat.None"/>.</summary>
    public static string? Rule(OdRepeat repeat, int interval, DateOnly? until, bool allDay)
    {
        if (repeat == OdRepeat.None)
        {
            return null;
        }

        var rule = string.Create(CultureInfo.InvariantCulture, $"FREQ={repeat.ToString().ToUpperInvariant()}");
        if (interval > 1)
        {
            rule += string.Create(CultureInfo.InvariantCulture, $";INTERVAL={interval}");
        }

        // A floating start needs a floating UNTIL, and an all-day one a date (RFC 5545 §3.3.10).
        if (until is { } last)
        {
            rule += allDay
                ? string.Create(CultureInfo.InvariantCulture, $";UNTIL={last:yyyyMMdd}")
                : string.Create(CultureInfo.InvariantCulture, $";UNTIL={last:yyyyMMdd}T235959");
        }

        return rule;
    }

    /// <summary>
    /// The editor's choices for <paramref name="rule"/>, or null when the rule says more than they can
    /// (an imported BYDAY or COUNT, for one); such a rule is kept as it is unless the user changes it.
    /// </summary>
    public static (OdRepeat Repeat, int Interval, DateOnly? Until)? Describe(string? rule)
    {
        if (rule is null)
        {
            return (OdRepeat.None, 1, null);
        }

        var pattern = new RecurrencePattern(rule);
        var simple = pattern.Count is null && pattern.ByDay.Count == 0 && pattern.ByMonthDay.Count == 0 && pattern.ByMonth.Count == 0
            && pattern.ByYearDay.Count == 0 && pattern.ByWeekNo.Count == 0 && pattern.BySetPosition.Count == 0
            && pattern.ByHour.Count == 0 && pattern.ByMinute.Count == 0 && pattern.BySecond.Count == 0;
        OdRepeat? repeat = pattern.Frequency switch
        {
            FrequencyType.Daily => OdRepeat.Daily,
            FrequencyType.Weekly => OdRepeat.Weekly,
            FrequencyType.Monthly => OdRepeat.Monthly,
            FrequencyType.Yearly => OdRepeat.Yearly,
            _ => null,
        };
        if (!simple || repeat is not { } known)
        {
            return null;
        }

        DateOnly? last = pattern.Until is { } until ? until.Date : null;
        return (known, pattern.Interval, last);
    }

    /// <summary>A local time as Ical.Net takes it: floating, or a bare date for an all-day event.</summary>
    internal static CalDateTime At(DateTime value, bool allDay) =>
        allDay ? new CalDateTime(DateOnly.FromDateTime(value)) : new CalDateTime(value, hasTime: true);
}
