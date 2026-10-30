namespace OmniDeck.Modules.Calculator;

/// <summary>One finished calculation in the session's history.</summary>
public sealed record OdCalcHistoryEntry(string Expression, string Result);
