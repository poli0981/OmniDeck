using System.Windows;
using System.Windows.Media;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// A line over bars on a time axis, as Weather draws the next hours' temperature over the chance of
/// rain: the line has a value axis on the left, and the bars fill 0 to 100 % of the plot's height.
/// </summary>
public sealed class OdLineBarChart : OdChartAxes
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<OdChartPoint>), typeof(OdLineBarChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(OdLineBarChart), new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(OdLineBarChart), new FrameworkPropertyMetadata(Brushes.LightSteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValueSuffixProperty = DependencyProperty.Register(
        nameof(ValueSuffix), typeof(string), typeof(OdLineBarChart), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public OdLineBarChart()
    {
        SetResourceReference(LineBrushProperty, "AccentFillColorDefaultBrush");
        SetResourceReference(BarBrushProperty, "ControlStrongStrokeColorDefaultBrush");
    }

    public IReadOnlyList<OdChartPoint>? Points
    {
        get => (IReadOnlyList<OdChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    /// <summary>What follows each value label, such as a degree sign.</summary>
    public string ValueSuffix
    {
        get => (string)GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    protected override int PointCount => Points?.Count ?? 0;

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Points is not { Count: > 0 } points || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Plot = Rect.Empty;
            return;
        }

        var scale = OdChartScale.Nice(points.Min(point => point.Line), points.Max(point => point.Line), MaxValueTicks);
        var axisWidth = AxisWidth(scale, ValueSuffix);
        var labelHeight = LabelHeight;
        Plot = new Rect(axisWidth, labelHeight / 2, Math.Max(1, ActualWidth - axisWidth), Math.Max(1, ActualHeight - (labelHeight * 2) - LabelGap));

        DrawValueAxis(drawingContext, scale, Plot, labelsOnRight: false, ValueSuffix);
        DrawBars(drawingContext, points);
        DrawLine(drawingContext, points, scale);
        DrawTimeAxis(drawingContext, [.. points.Select(point => point.Time)]);
        DrawCursor(drawingContext);
        if (CursorIndex >= 0 && CursorIndex < points.Count)
        {
            var radius = LineWeight * 2;
            drawingContext.DrawEllipse(LineBrush, null, PointAt(points, CursorIndex, scale), radius, radius);
        }
    }

    private void DrawBars(DrawingContext drawingContext, IReadOnlyList<OdChartPoint> points)
    {
        var slot = Plot.Width / points.Count;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Bar is not { } bar || bar <= 0)
            {
                continue;
            }

            var height = Plot.Height * Math.Min(bar, 100) / 100;
            drawingContext.DrawRectangle(BarBrush, null, new Rect(Plot.Left + (i * slot) + (slot * 0.2), Plot.Bottom - height, slot * 0.6, height));
        }
    }

    private void DrawLine(DrawingContext drawingContext, IReadOnlyList<OdChartPoint> points, OdChartScale scale)
    {
        if (points.Count == 1)
        {
            drawingContext.DrawEllipse(LineBrush, null, PointAt(points, 0, scale), LineWeight, LineWeight);
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(PointAt(points, 0, scale), isFilled: false, isClosed: false);
            for (var i = 1; i < points.Count; i++)
            {
                context.LineTo(PointAt(points, i, scale), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, Stroke(LineBrush), geometry);
    }

    private Point PointAt(IReadOnlyList<OdChartPoint> points, int index, OdChartScale scale) =>
        new(SlotCenter(index), scale.Map(points[index].Line, Plot.Bottom, Plot.Top));
}
