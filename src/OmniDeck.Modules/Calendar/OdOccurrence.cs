namespace OmniDeck.Modules.Calendar;

/// <summary>One occurrence of an event: the event itself for a one-off, one of its dates for a series.</summary>
public sealed record OdOccurrence(OdCalendarEvent Event, DateTime Start, DateTime End);
