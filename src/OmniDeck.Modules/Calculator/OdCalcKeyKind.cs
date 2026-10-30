namespace OmniDeck.Modules.Calculator;

/// <summary>What pressing a keypad key does.</summary>
public enum OdCalcKeyKind
{
    /// <summary>Adds the key's token to the expression.</summary>
    Input,

    Clear,

    Backspace,

    Negate,

    Evaluate,
}
