namespace OmniDeck.Core.Theming;

/// <summary>
/// The resolved <c>Od.*</c> design tokens the shell publishes as resources (docs/UI-MODES.md). Plain
/// numbers, so the arithmetic is testable without WPF; the app turns them into <c>Thickness</c>,
/// <c>CornerRadius</c> and <c>Duration</c> values.
/// </summary>
public sealed record OdTokens(
    string PresetName,
    double FontBase,
    double FontH1,
    double FontH2,
    double FontCaption,
    double Density,
    double HitTargetMin,
    double CornerRadius,
    int MotionMilliseconds,
    double ChartLineWeight,
    int NavMaxVisibleItems,
    bool NavShowLabels)
{
    /// <summary>The range of the user's font-size slider (docs/UI-MODES.md §3).</summary>
    public const double MinFontScale = 0.85;

    public const double MaxFontScale = 1.4;

    /// <summary>Windows text size runs from 100 % to 225 %.</summary>
    public const double MaxTextScale = 2.25;

    /// <summary>
    /// Applies the user's font scale and the Windows text size on top of <paramref name="preset"/>
    /// (docs/UI-MODES.md §3–4). Motion is off when the user or Windows turned it off, or the preset has none.
    /// </summary>
    public static OdTokens Compute(OdPreset preset, double textScale, double fontScale, bool motionEnabled)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var size = preset.FontBase * Math.Clamp(fontScale, MinFontScale, MaxFontScale) * Math.Clamp(textScale, 1, MaxTextScale);
        return new OdTokens(
            preset.Name,
            FontBase: Round(size),
            FontH1: Round(size * preset.FontScale * preset.FontScale),
            FontH2: Round(size * preset.FontScale),
            FontCaption: Round(size * 0.85),
            preset.Density,
            preset.HitTargetMin,
            preset.CornerRadius,
            MotionMilliseconds: motionEnabled ? preset.MotionMilliseconds : 0,
            preset.ChartLineWeight,
            preset.NavMaxVisibleItems,
            preset.NavShowLabels);
    }

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
