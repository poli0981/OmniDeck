namespace OmniDeck.Modules.Calculator;

/// <summary>One finished calculation in the session's history.</summary>
public sealed record OdCalcHistoryEntry(string Expression, string Result)
{
    /// <summary>The calculation as the history shows it, for screen readers.</summary>
    public override string ToString() => Expression + " = " + Result;
}
