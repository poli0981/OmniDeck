namespace OmniDeck.Modules.Calendar;

/// <summary>
/// An event in the user's calendar. Times are local wall-clock times ("floating" in iCalendar terms):
/// a 9:00 meeting stays at 9:00 whatever the PC's time zone.
/// </summary>
public sealed record OdCalendarEvent
{
    /// <summary>The database id; 0 until the event is saved.</summary>
    public long Id { get; init; }

    /// <summary>The iCalendar UID, kept across export and import.</summary>
    public required string Uid { get; init; }

    public required string Title { get; init; }

    public string Notes { get; init; } = "";

    public DateTime Start { get; init; }

    /// <summary>The end, exclusive; for an all-day event, the morning after its last day.</summary>
    public DateTime End { get; init; }

    public bool AllDay { get; init; }

    /// <summary>An iCalendar RRULE value such as <c>FREQ=WEEKLY;INTERVAL=2</c>; null for a one-off event.</summary>
    public string? Recurrence { get; init; }

    /// <summary>The starts of the occurrences removed from the series.</summary>
    public IReadOnlyList<DateTime> Exceptions { get; init; } = [];

    /// <summary>How many minutes before the start to remind; null for no reminder.</summary>
    public int? ReminderMinutes { get; init; }
}
