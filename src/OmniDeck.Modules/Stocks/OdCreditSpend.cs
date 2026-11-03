namespace OmniDeck.Modules.Stocks;

/// <summary>Credits spent at one moment.</summary>
public sealed record OdCreditSpend(DateTimeOffset At, int Credits);
