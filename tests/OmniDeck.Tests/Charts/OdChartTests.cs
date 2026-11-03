using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OmniDeck.Modules.Charts;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Charts;

// Labels follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdChartTests
{
    private static readonly DateTime Monday = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(0, 10, 6, 0, 10, 2)]
    [InlineData(3.7, 39.1, 6, 0, 40, 10)]
    [InlineData(0.31, 0.68, 5, 0.3, 0.7, 0.1)]
    [InlineData(-12.5, 7.3, 6, -15, 10, 5)]
    [InlineData(39.1, 3.7, 6, 0, 40, 10)]
    public void NiceScales_RoundTheRange_AndKeepToTheTickCount(double low, double high, int maxTicks, double min, double max, double step)
    {
        var scale = OdChartScale.Nice(low, high, maxTicks);

        Assert.Equal(min, scale.Min, 9);
        Assert.Equal(max, scale.Max, 9);
        Assert.Equal(step, scale.Step, 9);
        Assert.InRange(scale.Ticks.Count, 2, maxTicks);
        Assert.Equal(scale.Min, scale.Ticks[0], 9);
        Assert.Equal(scale.Max, scale.Ticks[^1], 9);
    }

    [Fact]
    public void NiceScales_GiveAFlatSeriesARange_AndSurviveNonsense()
    {
        var flat = OdChartScale.Nice(5, 5, 6);
        var nonsense = OdChartScale.Nice(double.NaN, 3, 6);

        Assert.True(flat.Min < 5 && flat.Max > 5);
        Assert.InRange(flat.Ticks.Count, 2, 6);
        Assert.Equal(1, flat.Decimals);
        Assert.Equal(new OdChartScale(0, 1, 0.5), nonsense);
        Assert.Equal(0, OdChartScale.Nice(0, 100, 6).Decimals);
        Assert.Equal(50, new OdChartScale(0, 10, 2).Map(5, 100, 0), 9);
    }

    [Fact]
    public void HourlyTicks_StepByHours_AndNameTheDayAtMidnight()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var hours = Enumerable.Range(0, 48).Select(i => Monday.AddHours(13 + i)).ToList();
        string At(int hour) => Monday.AddHours(hour).ToString("t", culture);

        var ticks = OdDateTicks.Choose(hours, 8, culture);

        Assert.Equal(new[] { At(18), "Tue", At(6), At(12), At(18), "Wed", At(6), At(12) }, ticks.Select(t => t.Label), StringComparer.Ordinal);
        Assert.Equal(5, ticks[0].Index);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("vi-VN")]
    [InlineData("ja-JP")]
    public void DailyTicks_MarkTheWeeks_InTheCulturesOwnShortWords(string culture)
    {
        var days = TradingDays(Monday, 22);
        var format = CultureInfo.GetCultureInfo(culture);

        var ticks = OdDateTicks.Choose(days, 6, format);

        Assert.InRange(ticks.Count, 3, 6);
        Assert.All(ticks, tick => Assert.Equal(DayOfWeek.Monday, days[tick.Index].DayOfWeek));
        Assert.Contains("9", ticks[0].Label, StringComparison.Ordinal);
        Assert.InRange(ticks[0].Label.Length, 3, 10);
    }

    [Fact]
    public void DailyTicks_InEnglish_ReadAsShortDates() =>
        Assert.Equal("Nov 9", OdDateTicks.Choose(TradingDays(Monday, 22), 6, CultureInfo.GetCultureInfo("en-US"))[0].Label);

    [Fact]
    public void LongSeries_FallBackToMonthsAndYears()
    {
        var weeks = Enumerable.Range(0, 261).Select(i => Monday.AddDays(-7 * (260 - i))).ToList();
        var year = TradingDays(Monday.AddYears(-1), 252);
        var culture = CultureInfo.GetCultureInfo("en-US");

        var yearly = OdDateTicks.Choose(weeks, 6, culture);
        var monthly = OdDateTicks.Choose(year, 12, culture);

        Assert.Equal(new[] { "2022", "2023", "2024", "2025", "2026" }, yearly.Select(t => t.Label), StringComparer.Ordinal);
        Assert.InRange(monthly.Count, 6, 12);
        Assert.Contains("2026", monthly.Select(t => t.Label), StringComparer.Ordinal);
        Assert.Empty(OdDateTicks.Choose([], 6, culture));
        Assert.Empty(OdDateTicks.Choose(year, 0, culture));
    }

    [Theory]
    [InlineData(-1, Key.Left, 10, 9)]
    [InlineData(-1, Key.Right, 10, 9)]
    [InlineData(-1, Key.Home, 10, 0)]
    [InlineData(4, Key.Left, 10, 3)]
    [InlineData(9, Key.Right, 10, 9)]
    [InlineData(0, Key.Left, 10, 0)]
    [InlineData(3, Key.End, 10, 9)]
    [InlineData(50, Key.PageUp, 100, 40)]
    [InlineData(95, Key.PageDown, 100, 99)]
    public void Keys_MoveTheCursor(int index, Key key, int count, int expected) =>
        Assert.Equal(expected, OdChartKeys.Move(index, key, count));

    [Fact]
    public void OtherKeys_AndEmptyCharts_LeaveTheCursorAlone()
    {
        Assert.Null(OdChartKeys.Move(3, Key.Tab, 10));
        Assert.Null(OdChartKeys.Move(-1, Key.Left, 0));
    }

    [Fact]
    public void Charts_DrawSomething_AtEveryDpi() => OdSta.Run(() =>
    {
        var bars = Enumerable.Range(0, 30)
            .Select(i => new OdOhlc(Monday.AddDays(i), 100 + i, 104 + i, 97 + i, i % 3 == 0 ? 98 + i : 102 + i, 1000 + (i * 50)))
            .ToList();
        var points = Enumerable.Range(0, 48).Select(i => new OdChartPoint(Monday.AddHours(i), 20 + (5 * Math.Sin(i / 4.0)), i % 5 * 20)).ToList();

        foreach (var dpi in new[] { 96.0, 216.0 })
        {
            Assert.True(Painted(new OdCandleChart { Bars = bars }, dpi) > 500);
            Assert.True(Painted(new OdCandleChart { Bars = bars, Kind = OdCandleKind.Bars }, dpi) > 500);
            Assert.True(Painted(new OdLineBarChart { Points = points, ValueSuffix = "°", CursorIndex = 10 }, dpi) > 500);
            Assert.True(Painted(new OdSparkline { Values = [.. points.Select(p => p.Line)] }, dpi) > 100);
        }

        Assert.Equal(0, Painted(new OdCandleChart(), 96));
        return Task.CompletedTask;
    });

    [Fact]
    public void ScreenReaders_HearTheSummary_AndTheValuesAtTheCursor() => OdSta.Run(() =>
    {
        var chart = new OdLineBarChart { Summary = "Next 48 hours: from 18° to 27°." };
        var peer = (OdChartAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(chart);

        Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
        Assert.Equal("Next 48 hours: from 18° to 27°.", peer.GetName());
        Assert.NotEmpty(peer.GetHelpText());
        Assert.Equal(nameof(OdLineBarChart), peer.GetClassName());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
        Assert.True(value.IsReadOnly);
        Assert.Equal("", value.Value);

        chart.Readout = "Monday at 7:00 PM: 25°";
        Assert.Equal("Monday at 7:00 PM: 25°", value.Value);
        Assert.Throws<ElementNotEnabledException>(() => value.SetValue("x"));

        AutomationProperties.SetName(chart, "Temperature");
        Assert.Equal("Temperature", peer.GetName());
        Assert.Null(UIElementAutomationPeer.CreatePeerForElement(new OdSparkline()).GetPattern(PatternInterface.Value));
        Assert.False(UIElementAutomationPeer.CreatePeerForElement(new OdSparkline()).IsControlElement());
        return Task.CompletedTask;
    });

    /// <summary>Weekdays from <paramref name="start"/>, as a stock's daily bars are.</summary>
    private static List<DateTime> TradingDays(DateTime start, int count)
    {
        var days = new List<DateTime>();
        var day = start;
        while (days.Count < count)
        {
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days.Add(day);
            }

            day = day.AddDays(1);
        }

        return days;
    }

    /// <summary>Lays <paramref name="chart"/> out at 400 × 240 and counts the pixels it paints at <paramref name="dpi"/>.</summary>
    private static int Painted(FrameworkElement chart, double dpi)
    {
        chart.Measure(new Size(400, 240));
        chart.Arrange(new Rect(0, 0, 400, 240));
        var scale = dpi / 96;
        var bitmap = new RenderTargetBitmap((int)(400 * scale), (int)(240 * scale), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(chart);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        var painted = 0;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                painted++;
            }
        }

        return painted;
    }
}
