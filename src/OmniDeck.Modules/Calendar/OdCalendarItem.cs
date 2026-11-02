namespace OmniDeck.Modules.Calendar;

/// <summary>An occurrence as a calendar row shows it: its time (or "All day") and its title.</summary>
public sealed record OdCalendarItem(string Time, string Title, OdOccurrence Occurrence)
{
    /// <summary>What screen readers announce for the row.</summary>
    public override string ToString() => Time + " " + Title;
}
