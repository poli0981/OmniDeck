using System.Windows;
using System.Windows.Media;
using OmniDeck.Core.Theming;

namespace OmniDeck.App.Theming;

/// <summary>
/// Turns resolved tokens into the preset layer: one <see cref="ResourceDictionary"/> merged after
/// WPF-UI's dictionaries and replaced in place when the preset, the user's overrides, the Windows text
/// size or the theme change (docs/spikes/05-theme-swap.md). On the light and dark themes it also carries
/// the preset's text palette (<see cref="OdPalette"/>); in High Contrast, Windows' colors stay.
/// </summary>
internal static class OdThemeResources
{
    public static ResourceDictionary Build(OdTokens tokens, OdThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var d = tokens.Density;
        var controlPadding = new Thickness(d * 2, d, d * 2, d);
        var layer = new ResourceDictionary
        {
            [OdTokenKeys.PresetMarker] = tokens.PresetName,
            [OdTokenKeys.FontBase] = tokens.FontBase,
            [OdTokenKeys.FontH1] = tokens.FontH1,
            [OdTokenKeys.FontH2] = tokens.FontH2,
            [OdTokenKeys.FontCaption] = tokens.FontCaption,
            [OdTokenKeys.Density] = d,
            [OdTokenKeys.PaddingControl] = controlPadding,
            [OdTokenKeys.PaddingCard] = new Thickness(d * 3),
            [OdTokenKeys.MarginItem] = new Thickness(0, 0, 0, d * 2),
            [OdTokenKeys.MarginTile] = new Thickness(0, 0, d * 2, d * 2),
            [OdTokenKeys.MarginLeading] = new Thickness(d * 2, 0, 0, 0),
            [OdTokenKeys.MarginInline] = new Thickness(0, 0, d * 2, 0),
            [OdTokenKeys.MarginTight] = new Thickness(d / 2),
            [OdTokenKeys.HitTargetMin] = tokens.HitTargetMin,
            [OdTokenKeys.CornerRadius] = new CornerRadius(tokens.CornerRadius),
            [OdTokenKeys.MotionDuration] = new Duration(TimeSpan.FromMilliseconds(tokens.MotionMilliseconds)),
            [OdTokenKeys.ChartLineWeight] = tokens.ChartLineWeight,
            [OdTokenKeys.NavMaxVisibleItems] = tokens.NavMaxVisibleItems,
            [OdTokenKeys.NavShowLabels] = tokens.NavShowLabels,
            [OdTokenKeys.NavIconSize] = tokens.NavIconSize,
            [OdTokenKeys.ContrastTarget] = tokens.ContrastTarget,

            // Deliberate WPF-UI overrides (docs/WPFUI-CONVENTIONS.md §6): WPF-UI 4.3 templates read these
            // keys through DynamicResource, so stock controls follow the preset without a style of ours.
            ["ControlCornerRadius"] = new CornerRadius(tokens.CornerRadius),
            ["ControlContentThemeFontSize"] = tokens.FontBase,
            ["TextControlThemePadding"] = controlPadding,
            ["TextControlThemeMinHeight"] = tokens.HitTargetMin,
        };

        // WPF-UI's text brushes take their colors through StaticResource, so the brushes themselves are replaced.
        if (OdPalette.For(OdPreset.FromName(tokens.PresetName), variant) is { } palette)
        {
            layer["TextFillColorSecondaryBrush"] = Brush(palette.TextSecondary);
            layer["TextFillColorTertiaryBrush"] = Brush(palette.TextTertiary);
        }

        return layer;
    }

    private static SolidColorBrush Brush(uint argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        return brush;
    }

    /// <summary>Puts <paramref name="preset"/> in place of the current preset layer, or adds it last.</summary>
    public static void Apply(ResourceDictionary target, ResourceDictionary preset)
    {
        ArgumentNullException.ThrowIfNull(target);
        var merged = target.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Contains(OdTokenKeys.PresetMarker))
            {
                merged[i] = preset;
                return;
            }
        }

        merged.Add(preset);
    }
}
