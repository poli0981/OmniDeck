namespace ThemeLab;

/// <summary>One row set of the token table in docs/UI-MODES.md §2.</summary>
public sealed record Preset(
    string Name, double FontBase, double FontScale, double Density, double HitTargetMin, double Corner, int MotionMs, double ChartLineWeight)
{
    public static readonly Preset Dynamic = new("Dynamic", 13.5, 1.35, 4, 32, 8, 180, 1.5);
    public static readonly Preset Standard = new("Standard", 15.5, 1.30, 6, 36, 6, 100, 2);
    public static readonly Preset Comfort = new("Comfort", 19, 1.25, 10, 44, 6, 0, 3);

    public static readonly Preset[] All = [Dynamic, Standard, Comfort];
}

/// <summary>
/// Builds the preset layer in code (derived tokens need the OS text scale and user overrides anyway)
/// and swaps it in place inside Application.Resources.MergedDictionaries.
/// </summary>
public static class PresetEngine
{
    private const string MarkerKey = "Od.Preset.Name";

    public static ResourceDictionary Build(Preset p, double textScale)
    {
        var body = p.FontBase * textScale;
        var d = p.Density;
        return new ResourceDictionary
        {
            [MarkerKey] = p.Name,
            ["Od.Font.Body"] = body,
            ["Od.Font.H2"] = body * p.FontScale,
            ["Od.Font.H1"] = body * p.FontScale * p.FontScale,
            ["Od.Font.Caption"] = body * 0.85,
            ["Od.Padding.Control"] = new Thickness(d * 2, d, d * 2, d),
            ["Od.Padding.Card"] = new Thickness(d * 3),
            ["Od.Margin.Item"] = new Thickness(0, 0, 0, d * 2),
            ["Od.HitTarget.Min"] = p.HitTargetMin,
            ["Od.Corner.Radius"] = new CornerRadius(p.Corner),
            ["Od.Motion.Duration"] = new Duration(TimeSpan.FromMilliseconds(p.MotionMs)),
            ["Od.Chart.LineWeight"] = p.ChartLineWeight,

            // Deliberate WPF-UI overrides, to see which ones follow a runtime swap. In WPF-UI 4.3
            // templates ControlCornerRadius, ControlContentThemeFontSize and TextControlThemeMinHeight
            // are DynamicResource; ButtonPadding and CardPadding are StaticResource.
            ["ControlCornerRadius"] = new CornerRadius(p.Corner),
            ["ControlContentThemeFontSize"] = body,
            ["TextControlThemeMinHeight"] = p.HitTargetMin,
            ["ButtonPadding"] = new Thickness(d * 2, d, d * 2, d),
            ["CardPadding"] = new Thickness(d * 3),
        };
    }

    public static void Apply(ResourceDictionary preset)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Contains(MarkerKey))
            {
                merged[i] = preset;
                return;
            }
        }

        merged.Add(preset);
    }
}
