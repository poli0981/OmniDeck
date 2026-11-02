namespace OmniDeck.Modules.Media;

/// <summary>How a file opens.</summary>
/// <param name="From">Where playback starts.</param>
/// <param name="Play">Whether it plays at once, or stands paused at <paramref name="From"/>.</param>
/// <param name="SubtitleEncoding">The text encoding of its subtitles; empty lets libVLC decide.</param>
/// <param name="Subtitle">A subtitle file to show with it, if the user added one.</param>
internal sealed record OdMediaOpening(TimeSpan From, bool Play, string SubtitleEncoding, string? Subtitle);
