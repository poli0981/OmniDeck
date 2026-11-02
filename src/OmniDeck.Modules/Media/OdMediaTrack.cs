namespace OmniDeck.Modules.Media;

/// <summary>An audio or subtitle track as libVLC names it; id -1 is libVLC's "Disable".</summary>
public sealed record OdMediaTrack(int Id, string Name);
