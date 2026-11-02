namespace OmniDeck.Modules.Media;

/// <summary>An audio output device: libVLC's id for it, and Windows' name.</summary>
public sealed record OdMediaDevice(string Id, string Name);
