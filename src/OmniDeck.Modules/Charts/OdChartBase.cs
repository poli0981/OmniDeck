using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using OmniDeck.Core.Theming;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// What OmniDeck's charts share. They draw with WPF itself, so their brushes follow the theme (High
/// Contrast included) and their text the font tokens; their line weight is <c>Od.Chart.LineWeight</c>.
/// A cursor moves among the points with the mouse or the arrow keys, and screen readers hear
/// <see cref="Summary"/> as the chart's name and <see cref="Readout"/>, the values at the cursor, as
/// its value. The consumer writes both, in the user's language.
/// </summary>
public abstract class OdChartBase : FrameworkElement
{
    public static readonly DependencyProperty LineWeightProperty = DependencyProperty.Register(
        nameof(LineWeight), typeof(double), typeof(OdChartBase), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelFontSizeProperty = DependencyProperty.Register(
        nameof(LabelFontSize), typeof(double), typeof(OdChartBase), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(OdChartBase), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(OdChartBase), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CursorBrushProperty = DependencyProperty.Register(
        nameof(CursorBrush), typeof(Brush), typeof(OdChartBase), new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CursorIndexProperty = DependencyProperty.Register(
        nameof(CursorIndex), typeof(int), typeof(OdChartBase), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(string), typeof(OdChartBase), new PropertyMetadata(""));

    public static readonly DependencyProperty ReadoutProperty = DependencyProperty.Register(
        nameof(Readout), typeof(string), typeof(OdChartBase), new PropertyMetadata("", OnReadoutChanged));

    /// <summary>How tall a chart wants to be, in label heights, when its container leaves the height open.</summary>
    private const double DefaultHeightInLabels = 14;

    protected OdChartBase()
    {
        Focusable = true;
        SetResourceReference(LineWeightProperty, OdTokenKeys.ChartLineWeight);
        SetResourceReference(LabelFontSizeProperty, OdTokenKeys.FontCaption);
        SetResourceReference(TextBrushProperty, "TextFillColorSecondaryBrush");
        SetResourceReference(GridBrushProperty, "DividerStrokeColorDefaultBrush");
        SetResourceReference(CursorBrushProperty, "TextFillColorPrimaryBrush");
    }

    public double LineWeight
    {
        get => (double)GetValue(LineWeightProperty);
        set => SetValue(LineWeightProperty, value);
    }

    public double LabelFontSize
    {
        get => (double)GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public Brush GridBrush
    {
        get => (Brush)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush CursorBrush
    {
        get => (Brush)GetValue(CursorBrushProperty);
        set => SetValue(CursorBrushProperty, value);
    }

    /// <summary>The point the cursor is on, or -1 when there is none.</summary>
    public int CursorIndex
    {
        get => (int)GetValue(CursorIndexProperty);
        set => SetValue(CursorIndexProperty, value);
    }

    /// <summary>One sentence that sums the chart up; screen readers read it as the chart's name.</summary>
    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>The values at the cursor, in words; screen readers read it as the chart's value.</summary>
    public string Readout
    {
        get => (string)GetValue(ReadoutProperty);
        set => SetValue(ReadoutProperty, value);
    }

    /// <summary>How many points the cursor can move among.</summary>
    protected abstract int PointCount { get; }

    /// <summary>The plot area of the last render, which the mouse maps onto points; empty before the first.</summary>
    protected Rect Plot { get; set; } = Rect.Empty;

    /// <summary>The point under <paramref name="x"/>, from <see cref="Plot"/>; -1 outside it.</summary>
    protected int IndexAt(double x)
    {
        if (Plot.IsEmpty || PointCount == 0 || x < Plot.Left || x > Plot.Right)
        {
            return -1;
        }

        var index = (int)((x - Plot.Left) / (Plot.Width / PointCount));
        return Math.Clamp(index, 0, PointCount - 1);
    }

    /// <summary>The middle of point <paramref name="index"/>'s slot in <see cref="Plot"/>.</summary>
    protected double SlotCenter(int index) => Plot.Left + ((index + 0.5) * (Plot.Width / Math.Max(1, PointCount)));

    /// <summary>Text in the chart's font at the label size, measured for this monitor's DPI.</summary>
    protected FormattedText Text(string text, Brush? brush = null) => new(
        text,
        CultureInfo.CurrentCulture,
        FlowDirection,
        new Typeface(TextElement.GetFontFamily(this), TextElement.GetFontStyle(this), TextElement.GetFontWeight(this), TextElement.GetFontStretch(this)),
        LabelFontSize,
        brush ?? TextBrush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    /// <summary>A pen of <paramref name="brush"/>, <see cref="LineWeight"/> wide unless told otherwise.</summary>
    protected Pen Stroke(Brush brush, double? thickness = null)
    {
        var pen = new Pen(brush, thickness ?? LineWeight) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? DefaultHeightInLabels * LabelFontSize : availableSize.Height);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (Keyboard.Modifiers == ModifierKeys.None && OdChartKeys.Move(CursorIndex, e.Key, PointCount) is { } next)
        {
            CursorIndex = next;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && CursorIndex >= 0)
        {
            CursorIndex = -1;
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        if (IndexAt(e.GetPosition(this).X) is var index and >= 0)
        {
            CursorIndex = index;
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!IsKeyboardFocused)
        {
            CursorIndex = -1;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        if (!IsMouseOver)
        {
            CursorIndex = -1;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new OdChartAutomationPeer(this);

    /// <summary>Draws the cursor: a line across the plot through point <see cref="CursorIndex"/>.</summary>
    protected void DrawCursor(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (CursorIndex < 0 || CursorIndex >= PointCount || Plot.IsEmpty)
        {
            return;
        }

        var x = SlotCenter(CursorIndex);
        drawingContext.DrawLine(Stroke(CursorBrush, Math.Max(1, LineWeight / 2)), new Point(x, Plot.Top), new Point(x, Plot.Bottom));
    }

    private static void OnReadoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (UIElementAutomationPeer.FromElement((UIElement)d) is OdChartAutomationPeer peer
            && AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            peer.RaiseValueChanged((string)e.OldValue, (string)e.NewValue);
        }
    }
}
