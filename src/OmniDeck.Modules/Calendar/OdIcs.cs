using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using IcalCalendar = Ical.Net.Calendar;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// iCalendar (.ics) files, through Ical.Net (docs/SPEC.md §3). Imported times become local wall-clock
/// times: an event fixed to another time zone is moved to the PC's.
/// </summary>
internal static class OdIcs
{
    /// <summary>The events in an iCalendar file.</summary>
    /// <exception cref="FormatException">The text is not an iCalendar file.</exception>
    public static IReadOnlyList<OdCalendarEvent> Read(string text)
    {
        IcalCalendar? calendar;
        try
        {
            calendar = IcalCalendar.Load(text);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new FormatException("The file is not an iCalendar file.", ex);
        }

        return calendar is null
            ? throw new FormatException("The file is not an iCalendar file.")
            : [.. calendar.Events.Where(item => item.DtStart is not null).Select(FromIcal)];
    }

    /// <summary>An iCalendar file with <paramref name="events"/>.</summary>
    public static string Write(IEnumerable<OdCalendarEvent> events)
    {
        var calendar = new IcalCalendar { ProductId = "-//OmniDeck//Calendar//EN" };
        foreach (var value in events)
        {
            calendar.Events.Add(ToIcal(value));
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? "";
    }

    private static OdCalendarEvent FromIcal(CalendarEvent item)
    {
        var allDay = item.IsAllDay || !item.DtStart!.HasTime;
        var start = Local(item.DtStart!);
        var defaultLength = allDay ? TimeSpan.FromDays(1) : TimeSpan.Zero;
        var end = item.DtEnd is { } dtEnd ? Local(dtEnd) : start + (item.Duration?.ToTimeSpanUnspecified() ?? defaultLength);
        var before = item.Alarms.Select(alarm => alarm.Trigger?.Duration).FirstOrDefault(duration => duration is not null)?.ToTimeSpanUnspecified();
        int? reminder = before is { } offset && offset <= TimeSpan.Zero ? (int)-offset.TotalMinutes : null;
        return new OdCalendarEvent
        {
            Uid = string.IsNullOrWhiteSpace(item.Uid) ? Guid.NewGuid().ToString("N") : item.Uid,
            Title = item.Summary ?? "",
            Notes = item.Description ?? "",
            Start = start,
            End = end < start ? start : end,
            AllDay = allDay,
            Recurrence = item.RecurrenceRule?.ToString(),
            Exceptions = [.. item.ExceptionDates.GetAllDates().Select(Local)],
            ReminderMinutes = reminder,
        };
    }

    private static CalendarEvent ToIcal(OdCalendarEvent value)
    {
        var item = new CalendarEvent
        {
            Uid = value.Uid,
            Summary = value.Title,
            Description = value.Notes.Length > 0 ? value.Notes : null,
            DtStart = OdRecurrence.At(value.Start, value.AllDay),
            DtEnd = OdRecurrence.At(value.End, value.AllDay),
        };
        if (value.Recurrence is { } rule)
        {
            item.RecurrenceRule = new RecurrencePattern(rule);
        }

        item.ExceptionDates.AddRange(value.Exceptions.Select(exception => OdRecurrence.At(exception, value.AllDay)));
        if (value.ReminderMinutes is { } minutes)
        {
            item.Alarms.Add(new Alarm { Action = "DISPLAY", Description = value.Title, Trigger = new Trigger(-Duration.FromMinutes(minutes)) });
        }

        return item;
    }

    // Floating times and dates stay as written; times fixed to UTC or a time zone become local.
    private static DateTime Local(CalDateTime value) =>
        value.IsFloating || !value.HasTime ? value.Value : value.AsUtc.ToLocalTime();
}
