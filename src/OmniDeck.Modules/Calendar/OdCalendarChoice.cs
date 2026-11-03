using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Calendar;

/// <summary>A labelled choice in one of the editor's lists: a time, a repeat, an interval or a reminder.</summary>
public sealed class OdCalendarChoice<T>(T value, Func<string> label) : OdLocalizedOption(label)
{
    public T Value { get; } = value;
}
