namespace OmniDeck.Modules.Calculator;

/// <summary>One keypad key: its label, what it types or does, and the name screen readers announce.</summary>
/// <param name="Label">The text on the key.</param>
/// <param name="Token">What an <see cref="OdCalcKeyKind.Input"/> key adds to the expression.</param>
/// <param name="Kind">What pressing the key does.</param>
/// <param name="Name">The spoken name and tooltip of a key whose label is a symbol; null when the label says it.</param>
public sealed record OdCalcKey(string Label, string Token, OdCalcKeyKind Kind = OdCalcKeyKind.Input, string? Name = null)
{
    /// <summary>An input key that types its own label.</summary>
    public OdCalcKey(string label)
        : this(label, label)
    {
    }

    public string AccessibleName => Name ?? Label;

    public bool IsPrimary => Kind == OdCalcKeyKind.Evaluate;
}
