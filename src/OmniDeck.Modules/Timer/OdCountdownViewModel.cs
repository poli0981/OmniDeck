using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>A countdown as the Timer view shows it.</summary>
public sealed partial class OdCountdownViewModel(OdCountdown countdown) : ObservableObject
{
    [ObservableProperty]
    private string _remainingText = "";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private string _toggleText = "";

    public OdCountdown Countdown { get; } = countdown;

    public string Name => Countdown.Name;

    public void Update(DateTimeOffset now)
    {
        var remaining = Countdown.RemainingAt(now);
        RemainingText = OdDurationText.Format(remaining, roundUp: true);
        Progress = 1 - (remaining / Countdown.Duration);
        IsRunning = Countdown.IsRunning;
        IsFinished = Countdown.IsFinished;
        ToggleText = IsRunning ? OdLocalization.Get("Timer_Pause") : OdLocalization.Get("Timer_Start");
    }

    public override string ToString() => Name;
}
