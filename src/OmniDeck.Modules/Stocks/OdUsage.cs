namespace OmniDeck.Modules.Stocks;

/// <summary>What a Twelve Data key may spend and has spent: credits per minute and per day, and its plan.</summary>
public sealed record OdUsage(int MinuteUsed, int MinuteLimit, int DayUsed, int DayLimit, string Plan);
