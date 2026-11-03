namespace OmniDeck.Modules.Charts;

/// <summary>One point of a line-and-bars chart: its time, the line's value and, when there is one, the bar's (0 to 100).</summary>
public sealed record OdChartPoint(DateTime Time, double Line, double? Bar);
