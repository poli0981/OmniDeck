using System.Globalization;

namespace OmniDeck.Modules.Media;

/// <summary>The playback speeds the player offers, from a quarter to four times (docs/SPEC.md §3.6).</summary>
internal static class OdMediaSpeeds
{
    public const float Normal = 1f;

    public static IReadOnlyList<float> All { get; } = [0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 3f, 4f];

    public static float Clamp(float rate) => float.IsFinite(rate) ? Math.Clamp(rate, All[0], All[^1]) : Normal;

    /// <summary>"1.5×", in the regional number format.</summary>
    public static string Title(float rate) => string.Format(CultureInfo.CurrentCulture, "{0:0.##}×", rate);
}
