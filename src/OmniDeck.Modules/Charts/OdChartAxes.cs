using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OmniDeck.Modules.Charts;

/// <summary>
/// The axes OmniDeck's charts draw: a value axis with grid lines and labels on either side of the
/// plot, and a time axis under it. Labels use the regional format, as the rest of the app does.
/// </summary>
public abstract class OdChartAxes : OdChartBase
{
    /// <summary>How many labels a value axis aims for at most.</summary>
    protected const int MaxValueTicks = 6;

    /// <summary>The room between a label and what it labels.</summary>
    protected double LabelGap => LabelFontSize / 2;

    /// <summary>The height of one line of label text.</summary>
    protected double LabelHeight => Text("0").Height;

    /// <summary>A value as the axis labels it, with <paramref name="decimals"/> decimals.</summary>
    protected static string FormatValue(double value, int decimals) =>
        value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);

    /// <summary>How wide the labels of <paramref name="scale"/> are, gap included.</summary>
    protected double AxisWidth(OdChartScale scale, string suffix)
    {
        ArgumentNullException.ThrowIfNull(scale);
        return scale.Ticks.Max(tick => Text(FormatValue(tick, scale.Decimals) + suffix).Width) + LabelGap;
    }

    /// <summary>Draws grid lines across <paramref name="area"/> at the ticks of <paramref name="scale"/>, labelled on the left or the right.</summary>
    protected void DrawValueAxis(DrawingContext drawingContext, OdChartScale scale, Rect area, bool labelsOnRight, string suffix)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(scale);
        var grid = Stroke(GridBrush, 1);
        foreach (var tick in scale.Ticks)
        {
            var y = scale.Map(tick, area.Bottom, area.Top);
            drawingContext.DrawLine(grid, new Point(area.Left, y), new Point(area.Right, y));
            var label = Text(FormatValue(tick, scale.Decimals) + suffix);
            var x = labelsOnRight ? area.Right + LabelGap : area.Left - LabelGap - label.Width;
            drawingContext.DrawText(label, new Point(x, y - (label.Height / 2)));
        }
    }

    /// <summary>Labels the time axis under the plot with as many of the points' periods as fit.</summary>
    protected void DrawTimeAxis(DrawingContext drawingContext, IReadOnlyList<DateTime> times)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(times);
        var maxLabels = Math.Max(1, (int)(Plot.Width / (LabelFontSize * 7)));
        var y = Plot.Bottom + (LabelGap / 2);
        foreach (var tick in OdDateTicks.Choose(times, maxLabels, CultureInfo.CurrentCulture))
        {
            var label = Text(tick.Label);
            var x = Math.Clamp(SlotCenter(tick.Index) - (label.Width / 2), 0, Math.Max(0, ActualWidth - label.Width));
            drawingContext.DrawText(label, new Point(x, y));
        }
    }
}
