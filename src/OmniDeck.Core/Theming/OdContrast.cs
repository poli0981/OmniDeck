namespace OmniDeck.Core.Theming;

/// <summary>
/// WCAG 2.x contrast (https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio): relative luminance, the ratio
/// between two colors (1 to 21), and what a translucent color looks like over an opaque one, since
/// WPF-UI's text brushes are black or white at partial opacity.
/// </summary>
public static class OdContrast
{
    /// <summary>WCAG AA for body text.</summary>
    public const double Aa = 4.5;

    /// <summary>WCAG AAA for body text, Comfort's target (docs/UI-MODES.md §2).</summary>
    public const double Aaa = 7.0;

    /// <summary>What <paramref name="argb"/>, alpha included, looks like over <paramref name="background"/>.</summary>
    public static OdRgb Over(uint argb, OdRgb background)
    {
        var alpha = (argb >> 24) / 255.0;
        var color = OdRgb.FromArgb(argb);
        return new OdRgb(Blend(color.R, background.R), Blend(color.G, background.G), Blend(color.B, background.B));

        byte Blend(byte top, byte bottom) => (byte)Math.Round((top * alpha) + (bottom * (1 - alpha)), MidpointRounding.AwayFromZero);
    }

    public static double Luminance(OdRgb color) => (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    public static double Ratio(OdRgb a, OdRgb b)
    {
        var first = Luminance(a);
        var second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    /// <summary>The contrast of the translucent <paramref name="foreground"/> over <paramref name="background"/>.</summary>
    public static double Ratio(uint foreground, OdRgb background) => Ratio(Over(foreground, background), background);

    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
