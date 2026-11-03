namespace OmniDeck.Modules.Timer;

/// <summary>A one-click countdown: its length and its button label.</summary>
public sealed record OdTimerPreset(int Minutes, string Label)
{
    /// <summary>The label: screen readers name a list item after its data.</summary>
    public override string ToString() => Label;
}
