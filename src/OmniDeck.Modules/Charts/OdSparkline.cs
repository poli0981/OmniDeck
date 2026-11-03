using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using OmniDeck.Core.Theming;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// A small line that shows a series' shape, in the rising color when it ended at or above where it
/// started and the falling one otherwise. It is decoration: screen readers skip it, so the row it sits
/// in must say the same in words.
/// </summary>
public sealed class OdSparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(OdSparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineWeightProperty = DependencyProperty.Register(
        nameof(LineWeight), typeof(double), typeof(OdSparkline), new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UpBrushProperty = DependencyProperty.Register(
        nameof(UpBrush), typeof(Brush), typeof(OdSparkline), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DownBrushProperty = DependencyProperty.Register(
        nameof(DownBrush), typeof(Brush), typeof(OdSparkline), new FrameworkPropertyMetadata(Brushes.Firebrick, FrameworkPropertyMetadataOptions.AffectsRender));

    public OdSparkline()
    {
        SetResourceReference(LineWeightProperty, OdTokenKeys.ChartLineWeight);
        SetResourceReference(UpBrushProperty, "SystemFillColorSuccessBrush");
        SetResourceReference(DownBrushProperty, "SystemFillColorCriticalBrush");
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double LineWeight
    {
        get => (double)GetValue(LineWeightProperty);
        set => SetValue(LineWeightProperty, value);
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

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Values is not { Count: > 1 } values || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var low = values.Min();
        var high = values.Max();
        var span = high - low > 0 ? high - low : 1;
        var inset = LineWeight;
        var height = Math.Max(1, ActualHeight - (2 * inset));
        var step = Math.Max(0, ActualWidth - (2 * inset)) / (values.Count - 1);
        Point At(int i) => new(inset + (i * step), inset + height - ((values[i] - low) / span * height));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(At(0), isFilled: false, isClosed: false);
            for (var i = 1; i < values.Count; i++)
            {
                context.LineTo(At(i), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        var pen = new Pen(values[^1] >= values[0] ? UpBrush : DownBrush, LineWeight) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DecorationPeer(this);

    /// <summary>Keeps the line out of what screen readers walk through.</summary>
    private sealed class DecorationPeer(OdSparkline owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
