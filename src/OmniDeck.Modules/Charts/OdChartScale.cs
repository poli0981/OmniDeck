namespace OmniDeck.Modules.Charts;

/// <summary>
/// A value axis: a range widened to round numbers, with a tick every <see cref="Step"/> (1, 2 or 5
/// times a power of ten), so the labels read 0, 20, 40 rather than 3.7, 21.4, 39.1.
/// </summary>
/// <param name="Min">The lowest value on the axis, a multiple of <see cref="Step"/>.</param>
/// <param name="Max">The highest value on the axis, a multiple of <see cref="Step"/>.</param>
/// <param name="Step">The distance between two ticks.</param>
public sealed record OdChartScale(double Min, double Max, double Step)
{
    /// <summary>The ticks from <see cref="Min"/> to <see cref="Max"/>.</summary>
    public IReadOnlyList<double> Ticks
    {
        get
        {
            var count = (int)Math.Round((Max - Min) / Step) + 1;
            return [.. Enumerable.Range(0, count).Select(i => Math.Round(Min + (i * Step), Decimals + 1))];
        }
    }

    /// <summary>How many decimals the tick labels need: none for a step of 20, one for 0.5.</summary>
    public int Decimals => Math.Max(0, -(int)Math.Floor(Math.Log10(Step)));

    /// <summary>
    /// The round axis around <paramref name="low"/> to <paramref name="high"/>, with at most
    /// <paramref name="maxTicks"/> ticks. A flat series still gets a range around its value, and values
    /// that are not finite give the axis 0 to 1.
    /// </summary>
    public static OdChartScale Nice(double low, double high, int maxTicks)
    {
        if (!double.IsFinite(low) || !double.IsFinite(high))
        {
            return new OdChartScale(0, 1, 0.5);
        }

        if (high < low)
        {
            (low, high) = (high, low);
        }

        if (high - low < 1e-9)
        {
            var pad = Math.Max(Math.Abs(low) * 0.05, 1e-6);
            low -= pad;
            high += pad;
        }

        maxTicks = Math.Max(2, maxTicks);
        var step = RoundUp((high - low) / (maxTicks - 1));
        while (true)
        {
            var min = Math.Floor(low / step) * step;
            var max = Math.Ceiling(high / step) * step;
            if (Math.Round((max - min) / step) + 1 <= maxTicks)
            {
                return new OdChartScale(min, max, step);
            }

            step = Next(step);
        }
    }

    /// <summary>
    /// Where <paramref name="value"/> falls when <see cref="Min"/> is at <paramref name="from"/> and
    /// <see cref="Max"/> at <paramref name="to"/>, in pixels.
    /// </summary>
    public double Map(double value, double from, double to) => from + ((value - Min) / (Max - Min) * (to - from));

    /// <summary>The smallest round step (1, 2 or 5 times a power of ten) that is at least <paramref name="rough"/>.</summary>
    private static double RoundUp(double rough)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var fraction = rough / power;
        var mantissa = fraction switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10,
        };
        return mantissa * power;
    }

    /// <summary>The round step after <paramref name="step"/>: 1 → 2 → 5 → 10.</summary>
    private static double Next(double step)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(step) + 1e-9));
        var mantissa = Math.Round(step / power);
        var next = mantissa switch
        {
            < 2 => 2,
            < 5 => 5,
            _ => 10,
        };
        return next * power;
    }
}
