using Microsoft.Extensions.Time.Testing;
using OmniDeck.Modules.Timer;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.ModuleApps;

public sealed class OdTimerServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
    private readonly OdFakeNotifier _notifier = new();

    [Fact]
    public void ACountdown_NotifiesWhenItRunsOut() => OdSta.Run(async () =>
    {
        using var service = new OdTimerService(_time, _notifier);
        var tea = service.Add("Tea", TimeSpan.FromMinutes(3));
        service.Start(tea);

        _time.Advance(TimeSpan.FromMinutes(2));
        await Task.Yield();
        Assert.Equal(TimeSpan.FromMinutes(1), tea.RemainingAt(service.Now));
        Assert.Empty(_notifier.Shown);

        _time.Advance(TimeSpan.FromMinutes(1));
        await Task.Yield();
        Assert.True(tea.IsFinished);
        Assert.False(tea.IsRunning);
        Assert.Equal(TimeSpan.Zero, tea.RemainingAt(service.Now));
        Assert.Equal(new[] { ("Timer finished", "Tea is done.") }, _notifier.Shown);
    });

    [Fact]
    public void PauseResumeAndReset_KeepTheRemainingTime() => OdSta.Run(async () =>
    {
        using var service = new OdTimerService(_time, _notifier);
        var countdown = service.Add("Pasta", TimeSpan.FromMinutes(10));
        service.Start(countdown);
        service.Start(countdown);

        _time.Advance(TimeSpan.FromMinutes(4));
        service.Pause(countdown);
        service.Pause(countdown);
        _time.Advance(TimeSpan.FromHours(1));
        await Task.Yield();
        Assert.Equal(TimeSpan.FromMinutes(6), countdown.RemainingAt(service.Now));
        Assert.Empty(_notifier.Shown);

        service.Start(countdown);
        _time.Advance(TimeSpan.FromMinutes(6));
        await Task.Yield();
        Assert.True(countdown.IsFinished);

        // Starting a finished countdown runs the full duration again.
        service.Start(countdown);
        Assert.Equal(TimeSpan.FromMinutes(10), countdown.RemainingAt(service.Now));
        service.Reset(countdown);
        Assert.False(countdown.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(10), countdown.Remaining);
    });

    [Fact]
    public void SeveralCountdowns_FinishInTheirOwnOrder_AndRemovedOnesStaySilent() => OdSta.Run(async () =>
    {
        using var service = new OdTimerService(_time, _notifier);
        var changes = 0;
        service.Changed += (_, _) => changes++;
        var longer = service.Add("Long", TimeSpan.FromMinutes(5));
        var shorter = service.Add("Short", TimeSpan.FromMinutes(1));
        var removed = service.Add("Gone", TimeSpan.FromMinutes(2));
        service.Start(longer);
        service.Start(shorter);
        service.Start(removed);
        service.Remove(removed);

        _time.Advance(TimeSpan.FromMinutes(1));
        await Task.Yield();
        _time.Advance(TimeSpan.FromMinutes(4));
        await Task.Yield();

        Assert.Equal(new[] { "Short is done.", "Long is done." }, _notifier.Shown.Select(n => n.Message), StringComparer.Ordinal);
        Assert.Equal(new[] { longer, shorter }, service.Countdowns);
        Assert.True(changes > 6);
    });

    [Fact]
    public void QuickTimer_StartsAtOnce()
    {
        using var service = new OdTimerService(_time, _notifier);

        var quick = ((IOdQuickTimer)service).StartQuick(TimeSpan.FromMinutes(5));

        Assert.Equal("Quick timer", quick.Name);
        Assert.True(quick.IsRunning);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Add("Zero", TimeSpan.Zero));
    }

    [Fact]
    public void AnAlarmWithoutAUiContext_FiresInline()
    {
        using var service = RunWithoutContext(() => new OdTimerService(_time, _notifier));
        service.Start(service.Add("Egg", TimeSpan.FromSeconds(30)));

        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.Single(_notifier.Shown);
    }

    [Fact]
    public void Stopwatch_BanksTimeAcrossStops_AndRecordsLaps()
    {
        using var service = new OdTimerService(_time, _notifier);

        service.StartStopwatch();
        _time.Advance(TimeSpan.FromSeconds(10));
        service.Lap();
        _time.Advance(TimeSpan.FromSeconds(5));
        service.StopStopwatch();
        service.Lap();
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromSeconds(15), service.Stopwatch.ElapsedAt(service.Now));

        service.StartStopwatch();
        _time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(18), service.Stopwatch.ElapsedAt(service.Now));
        Assert.Equal(new[] { TimeSpan.FromSeconds(10) }, service.Stopwatch.Laps);

        service.ResetStopwatch();
        Assert.False(service.Stopwatch.IsRunning);
        Assert.Equal(TimeSpan.Zero, service.Stopwatch.ElapsedAt(service.Now));
        Assert.Empty(service.Stopwatch.Laps);
    }

    [Theory]
    [InlineData(0, false, "0:00")]
    [InlineData(59.2, true, "1:00")]
    [InlineData(59.2, false, "0:59")]
    [InlineData(299, false, "4:59")]
    [InlineData(3723, false, "1:02:03")]
    [InlineData(-5, false, "0:00")]
    public void Durations_ReadLikeATimer(double seconds, bool roundUp, string expected)
    {
        Assert.Equal(expected, OdDurationText.Format(TimeSpan.FromSeconds(seconds), roundUp));
    }

    [Fact]
    public void StopwatchDurations_ShowHundredths()
    {
        Assert.Equal("1:05.42", OdDurationText.FormatPrecise(TimeSpan.FromMilliseconds(65_429)));
        Assert.Equal("0:00.00", OdDurationText.FormatPrecise(TimeSpan.Zero));
    }

    private static T RunWithoutContext<T>(Func<T> create)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return create();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
