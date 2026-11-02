namespace OmniDeck.Modules.Calendar;

/// <summary>A labelled choice in one of the editor's lists: a time, a repeat or a reminder.</summary>
public sealed record OdCalendarChoice<T>(T Value, string Label)
{
    /// <summary>What the list shows, and what screen readers announce.</summary>
    public override string ToString() => Label;
}
