namespace OmniDeck.Modules.Media;

/// <summary>The media module's settings document: the volume, and whether the queue panel shows.</summary>
public sealed record OdMediaSettings
{
    public int Volume { get; init; } = 80;

    public bool Muted { get; init; }

    public bool QueueVisible { get; init; } = true;
}
