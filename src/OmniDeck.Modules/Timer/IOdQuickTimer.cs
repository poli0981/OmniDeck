namespace OmniDeck.Modules.Timer;

/// <summary>Starts a countdown from outside the Timer module: the tray's "quick timer" (docs/SPEC.md §2).</summary>
public interface IOdQuickTimer
{
    OdCountdown StartQuick(TimeSpan duration);
}
