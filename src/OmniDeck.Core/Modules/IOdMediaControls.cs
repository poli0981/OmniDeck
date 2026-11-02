namespace OmniDeck.Core.Modules;

/// <summary>
/// A module that plays media, which the tray menu controls while something is loaded
/// (docs/SPEC.md §2: "media controls when playing").
/// </summary>
public interface IOdMediaControls
{
    /// <summary>What is loaded and ready to play or pause, or null when nothing is.</summary>
    string? NowPlaying { get; }

    bool IsPlaying { get; }

    void PlayPause();

    void Next();

    void Previous();
}
