namespace OmniDeck.Core.Settings;

/// <summary>The dark/light axis, independent of the UI preset (docs/UI-MODES.md §6).</summary>
public enum OdThemeMode
{
    /// <summary>Follow the Windows app mode (the default).</summary>
    System,

    Light,

    Dark,
}
