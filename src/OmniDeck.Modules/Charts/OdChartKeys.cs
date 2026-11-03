using System.Windows.Input;

namespace OmniDeck.Modules.Charts;

/// <summary>How the arrow, Home, End and page keys move a chart's cursor among its points.</summary>
public static class OdChartKeys
{
    /// <summary>
    /// Where the cursor at <paramref name="index"/> goes for <paramref name="key"/> among
    /// <paramref name="count"/> points, or null for a key the chart leaves alone. Without a cursor yet
    /// (-1), the first move lands on the latest point, which is what a reader usually wants first.
    /// </summary>
    public static int? Move(int index, Key key, int count)
    {
        if (count <= 0)
        {
            return null;
        }

        var page = Math.Max(1, count / 10);
        var last = count - 1;
        int? next = key switch
        {
            Key.Home => 0,
            Key.End => last,
            Key.Left or Key.Right or Key.PageUp or Key.PageDown when index < 0 => last,
            Key.Left => index - 1,
            Key.Right => index + 1,
            Key.PageUp => index - page,
            Key.PageDown => index + page,
            _ => null,
        };
        return next is { } position ? Math.Clamp(position, 0, last) : null;
    }
}
