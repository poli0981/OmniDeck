namespace OmniDeck.Modules.Timer;

/// <summary>One stopwatch lap: its title ("Lap 3"), its own length and the total at that moment.</summary>
public sealed record OdLapViewModel(string Title, string LapText, string TotalText);
