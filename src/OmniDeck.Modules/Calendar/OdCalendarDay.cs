namespace OmniDeck.Modules.Calendar;

/// <summary>
/// A day in the month grid or the lists: its label, and up to a few of its events; <c>More</c> says
/// "+N more" when the cell shows only some of them.
/// </summary>
public sealed record OdCalendarDay(DateOnly Date, string Label, bool IsInPeriod, bool IsToday, IReadOnlyList<OdCalendarItem> Items, string? More)
{
    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => Items.Count == 0;

    public bool HasMore => More is not null;
}
