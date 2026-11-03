namespace OmniDeck.Modules.Timer;

/// <summary>A repeat choice, with its name in the UI language.</summary>
public sealed record OdAlarmRepeatOption(OdAlarmRepeat Repeat, string Label)
{
    public override string ToString() => Label;
}
