namespace OmniDeck.Core.Theming;

/// <summary>
/// The resource keys of the <c>Od.*</c> tokens. Views consume them through <c>DynamicResource</c>;
/// a literal font size, padding, radius or animation duration in a module view is a CI failure.
/// </summary>
public static class OdTokenKeys
{
    public const string FontBase = "Od.Font.Base";
    public const string FontH1 = "Od.Font.H1";
    public const string FontH2 = "Od.Font.H2";
    public const string FontCaption = "Od.Font.Caption";
    public const string Density = "Od.Density";
    public const string PaddingControl = "Od.Padding.Control";
    public const string PaddingCard = "Od.Padding.Card";
    public const string MarginItem = "Od.Margin.Item";

    /// <summary>The gap after a tile in a wrapping grid, such as the module library.</summary>
    public const string MarginTile = "Od.Margin.Tile";

    /// <summary>The gap before an element in a row, such as the title bar's search box after the title.</summary>
    public const string MarginLeading = "Od.Margin.Leading";

    /// <summary>The gap after an element in a row of text or icons.</summary>
    public const string MarginInline = "Od.Margin.Inline";

    /// <summary>Half a density unit all round, between the keys of a keypad.</summary>
    public const string MarginTight = "Od.Margin.Tight";

    public const string HitTargetMin = "Od.HitTarget.Min";
    public const string CornerRadius = "Od.Corner.Radius";
    public const string MotionDuration = "Od.Motion.Duration";
    public const string ChartLineWeight = "Od.Chart.LineWeight";
    public const string NavMaxVisibleItems = "Od.Nav.MaxVisibleItems";
    public const string NavShowLabels = "Od.Nav.ShowLabels";

    /// <summary>
    /// The opacity of what is dimmed but still in use, such as the days outside the month: 0.7 keeps
    /// primary text above 4.5:1 on the light theme, and High Contrast dims nothing.
    /// </summary>
    public const string DimOpacity = "Od.Opacity.Dim";

    /// <summary>Widths that grow with the text (<see cref="OdTokens.TileWidth"/> and the next ones).</summary>
    public const string WidthTile = "Od.Width.Tile";

    public const string WidthTileWide = "Od.Width.TileWide";

    public const string WidthField = "Od.Width.Field";

    public const string WidthReadable = "Od.Width.Readable";

    /// <summary>The size of the sidebar's icons.</summary>
    public const string NavIconSize = "Od.Nav.IconSize";

    /// <summary>The contrast secondary and tertiary text meet: 4.5 (AA) or 7 (AAA, Comfort).</summary>
    public const string ContrastTarget = "Od.Contrast.Target";

    /// <summary>Marks the preset dictionary inside the merged dictionaries; its value is the preset name.</summary>
    public const string PresetMarker = "Od.Preset";
}
