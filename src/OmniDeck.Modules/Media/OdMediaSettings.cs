namespace OmniDeck.Modules.Media;

/// <summary>The media module's settings document: volume, speed, subtitle encoding, audio device, and whether the queue panel shows.</summary>
public sealed record OdMediaSettings
{
    public int Volume { get; init; } = 80;

    public bool Muted { get; init; }

    public bool QueueVisible { get; init; } = true;

    public float Rate { get; init; } = 1f;

    /// <summary>libVLC's name for the subtitles' encoding; empty lets libVLC decide.</summary>
    public string SubtitleEncoding { get; init; } = "";

    /// <summary>libVLC's id for the audio output device; null is Windows' default.</summary>
    public string? AudioDevice { get; init; }
}
