using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>A repeat choice, with its name in the UI language.</summary>
public sealed class OdAlarmRepeatOption(OdAlarmRepeat repeat, Func<string> label) : OdLocalizedOption(label)
{
    public OdAlarmRepeat Repeat { get; } = repeat;
}
