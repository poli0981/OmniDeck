using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>One stopwatch lap: its title ("Lap 3"), its own length and the total at that moment.</summary>
public sealed record OdLapViewModel(string Title, string LapText, string TotalText)
{
    /// <summary>The lap as one sentence: screen readers name a list item after its data.</summary>
    public override string ToString() => OdLocalization.Format("Timer_LapName", Title, LapText, TotalText);
}
