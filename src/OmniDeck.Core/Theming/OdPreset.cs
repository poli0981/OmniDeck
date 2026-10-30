namespace OmniDeck.Core.Theming;

/// <summary>
/// One column of the token table in docs/UI-MODES.md §2: the base values a UI preset starts from
/// before the user's overrides and the Windows text size are applied (see <see cref="OdTokens"/>).
/// </summary>
public sealed record OdPreset(
    string Name,
    double FontBase,
    double FontScale,
    double Density,
    double HitTargetMin,
    double CornerRadius,
    int MotionMilliseconds,
    double ChartLineWeight,
    int NavMaxVisibleItems,
    bool NavShowLabels)
{
    /// <summary>Dense, vivid, animated.</summary>
    public static readonly OdPreset Dynamic = new("Dynamic", 13.5, 1.35, 4, 32, 8, 180, 1.5, 0, NavShowLabels: false);

    /// <summary>The default, balanced.</summary>
    public static readonly OdPreset Standard = new("Standard", 15.5, 1.30, 6, 36, 6, 100, 2, 0, NavShowLabels: true);

    /// <summary>Large type, calm, no motion.</summary>
    public static readonly OdPreset Comfort = new("Comfort", 19, 1.25, 10, 44, 6, 0, 3, 6, NavShowLabels: true);

    public static IReadOnlyList<OdPreset> All { get; } = [Dynamic, Standard, Comfort];

    /// <summary>The preset with this name, or <see cref="Standard"/>.</summary>
    public static OdPreset FromName(string? name) =>
        All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Standard;
}
