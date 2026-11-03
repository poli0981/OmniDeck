using System.Windows;
using System.Windows.Media;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// A price chart: candles or OHLC bars over a time axis, with the price axis on the right and, when
/// the bars carry it, the volume in a band below. A rising candle is hollow and a falling one filled,
/// so the direction does not rest on color alone.
/// </summary>
public sealed class OdCandleChart : OdChartAxes
{
    public static readonly DependencyProperty BarsProperty = DependencyProperty.Register(
        nameof(Bars), typeof(IReadOnlyList<OdOhlc>), typeof(OdCandleChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(OdCandleKind), typeof(OdCandleChart), new FrameworkPropertyMetadata(OdCandleKind.Candles, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UpBrushProperty = DependencyProperty.Register(
        nameof(UpBrush), typeof(Brush), typeof(OdCandleChart), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DownBrushProperty = DependencyProperty.Register(
        nameof(DownBrush), typeof(Brush), typeof(OdCandleChart), new FrameworkPropertyMetadata(Brushes.Firebrick, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VolumeBrushProperty = DependencyProperty.Register(
        nameof(VolumeBrush), typeof(Brush), typeof(OdCandleChart), new FrameworkPropertyMetadata(Brushes.Silver, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The share of the plot's height the prices take when a volume band is drawn below them.</summary>
    private const double PriceShare = 0.75;

    public OdCandleChart()
    {
        SetResourceReference(UpBrushProperty, "SystemFillColorSuccessBrush");
        SetResourceReference(DownBrushProperty, "SystemFillColorCriticalBrush");
        SetResourceReference(VolumeBrushProperty, "ControlStrongStrokeColorDefaultBrush");
    }

    public IReadOnlyList<OdOhlc>? Bars
    {
        get => (IReadOnlyList<OdOhlc>?)GetValue(BarsProperty);
        set => SetValue(BarsProperty, value);
    }

    public OdCandleKind Kind
    {
        get => (OdCandleKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush UpBrush
    {
        get => (Brush)GetValue(UpBrushProperty);
        set => SetValue(UpBrushProperty, value);
    }

    public Brush DownBrush
    {
        get => (Brush)GetValue(DownBrushProperty);
        set => SetValue(DownBrushProperty, value);
    }

    public Brush VolumeBrush
    {
        get => (Brush)GetValue(VolumeBrushProperty);
        set => SetValue(VolumeBrushProperty, value);
    }

    protected override int PointCount => Bars?.Count ?? 0;

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Bars is not { Count: > 0 } bars || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Plot = Rect.Empty;
            return;
        }

        var scale = OdChartScale.Nice(bars.Min(bar => bar.Low), bars.Max(bar => bar.High), MaxValueTicks);
        var axisWidth = AxisWidth(scale, "");
        var labelHeight = LabelHeight;
        Plot = new Rect(0, labelHeight / 2, Math.Max(1, ActualWidth - axisWidth), Math.Max(1, ActualHeight - (labelHeight * 2) - LabelGap));

        var hasVolume = bars.Any(bar => bar.Volume > 0);
        var prices = hasVolume ? new Rect(Plot.Left, Plot.Top, Plot.Width, Plot.Height * PriceShare) : Plot;
        DrawValueAxis(drawingContext, scale, prices, labelsOnRight: true, "");
        if (hasVolume)
        {
            DrawVolume(drawingContext, bars, new Rect(Plot.Left, prices.Bottom + LabelGap, Plot.Width, Math.Max(1, Plot.Bottom - prices.Bottom - LabelGap)));
        }

        var pens = (Up: Stroke(UpBrush, Math.Max(1, LineWeight / 2)), Down: Stroke(DownBrush, Math.Max(1, LineWeight / 2)));
        for (var i = 0; i < bars.Count; i++)
        {
            DrawBar(drawingContext, bars[i], SlotCenter(i), scale, prices, bars[i].IsUp ? pens.Up : pens.Down);
        }

        DrawTimeAxis(drawingContext, [.. bars.Select(bar => bar.Time)]);
        DrawCursor(drawingContext);
    }

    private void DrawBar(DrawingContext drawingContext, OdOhlc bar, double x, OdChartScale scale, Rect area, Pen pen)
    {
        double Y(double value) => scale.Map(value, area.Bottom, area.Top);
        var width = Math.Max(1, Plot.Width / PointCount * 0.6);
        if (Kind == OdCandleKind.Bars)
        {
            drawingContext.DrawLine(pen, new Point(x, Y(bar.High)), new Point(x, Y(bar.Low)));
            drawingContext.DrawLine(pen, new Point(x - (width / 2), Y(bar.Open)), new Point(x, Y(bar.Open)));
            drawingContext.DrawLine(pen, new Point(x, Y(bar.Close)), new Point(x + (width / 2), Y(bar.Close)));
            return;
        }

        var top = Y(Math.Max(bar.Open, bar.Close));
        var bottom = Y(Math.Min(bar.Open, bar.Close));
        drawingContext.DrawLine(pen, new Point(x, Y(bar.High)), new Point(x, top));
        drawingContext.DrawLine(pen, new Point(x, bottom), new Point(x, Y(bar.Low)));

        // Too narrow to show a hollow body: a rising candle is filled too, and its outline still tells.
        var hollow = bar.IsUp && width >= 3;
        drawingContext.DrawRectangle(hollow ? null : pen.Brush, pen, new Rect(x - (width / 2), top, width, Math.Max(1, bottom - top)));
    }

    private void DrawVolume(DrawingContext drawingContext, IReadOnlyList<OdOhlc> bars, Rect area)
    {
        var most = bars.Max(bar => bar.Volume ?? 0);
        var width = Math.Max(1, area.Width / bars.Count * 0.6);
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i].Volume is { } volume && volume > 0)
            {
                var height = area.Height * volume / most;
                drawingContext.DrawRectangle(VolumeBrush, null, new Rect(SlotCenter(i) - (width / 2), area.Bottom - height, width, height));
            }
        }
    }
}
