namespace OmniDeck.Modules.Charts;

/// <summary>A label on a time axis, under the point at <paramref name="Index"/>.</summary>
public sealed record OdTick(int Index, string Label);
