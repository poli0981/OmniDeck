namespace OmniDeck.Core.Theming;

/// <summary>
/// The text colors a preset puts over WPF-UI's (docs/UI-MODES.md §2, §6), as <c>#AARRGGBB</c>, so that
/// secondary and tertiary text meet the preset's contrast target on every surface: AA (4.5:1) for
/// Dynamic and Standard, AAA (7:1) for Comfort. WPF-UI 4.3's own tertiary text on the light theme is
/// about 3.3:1. High Contrast has no palette of ours: Windows' colors rule there (§4).
/// </summary>
public sealed record OdPalette(uint TextSecondary, uint TextTertiary)
{
    // WPF-UI 4.3's light and dark values, which Dynamic and Standard keep where they already pass.
    private const uint StockLightSecondary = 0x9E000000;
    private const uint StockDarkSecondary = 0xC5FFFFFF;
    private const uint StockDarkTertiary = 0x87FFFFFF;

    /// <summary>The palette for <paramref name="preset"/> on <paramref name="variant"/>; null in High Contrast.</summary>
    public static OdPalette? For(OdPreset preset, OdThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var comfort = preset.ContrastTarget >= OdContrast.Aaa;
        return variant switch
        {
            OdThemeVariant.Light when comfort => new OdPalette(0xC8000000, 0xB0000000),
            OdThemeVariant.Light => new OdPalette(StockLightSecondary, 0x8F000000),
            OdThemeVariant.Dark when comfort => new OdPalette(0xE6FFFFFF, 0xB8FFFFFF),
            OdThemeVariant.Dark => new OdPalette(StockDarkSecondary, StockDarkTertiary),
            _ => null,
        };
    }
}
