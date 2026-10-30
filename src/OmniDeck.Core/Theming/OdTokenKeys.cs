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
    public const string HitTargetMin = "Od.HitTarget.Min";
    public const string CornerRadius = "Od.Corner.Radius";
    public const string MotionDuration = "Od.Motion.Duration";
    public const string ChartLineWeight = "Od.Chart.LineWeight";
    public const string NavMaxVisibleItems = "Od.Nav.MaxVisibleItems";
    public const string NavShowLabels = "Od.Nav.ShowLabels";

    /// <summary>Marks the preset dictionary inside the merged dictionaries; its value is the preset name.</summary>
    public const string PresetMarker = "Od.Preset";
}
