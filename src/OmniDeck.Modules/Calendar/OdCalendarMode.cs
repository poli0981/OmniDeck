namespace OmniDeck.Modules.Calendar;

/// <summary>How the calendar shows its events.</summary>
public enum OdCalendarMode
{
    /// <summary>A six-week grid around the month.</summary>
    Month,

    /// <summary>The seven days of a week, as a list.</summary>
    Week,

    /// <summary>The next 30 days that have events.</summary>
    Agenda,
}
