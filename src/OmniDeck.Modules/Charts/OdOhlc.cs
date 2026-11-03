namespace OmniDeck.Modules.Charts;

/// <summary>One bar of a price series: its time, open, high, low and close, and the volume when it is known.</summary>
public sealed record OdOhlc(DateTime Time, double Open, double High, double Low, double Close, double? Volume)
{
    /// <summary>Whether the price closed at or above where it opened.</summary>
    public bool IsUp => Close >= Open;
}
