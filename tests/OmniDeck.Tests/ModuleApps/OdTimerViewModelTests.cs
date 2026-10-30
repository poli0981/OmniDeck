using Microsoft.Extensions.Time.Testing;
using OmniDeck.Modules.Timer;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.ModuleApps;

public sealed class OdTimerViewModelTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public void AddCountdown_StartsItWithAName() => OdSta.Run(() =>
    {
        using var service = new OdTimerService(_time, new OdFakeNotifier());
        using var timer = new OdTimerViewModel(service, _time) { NewMinutes = 2, NewSeconds = 30 };

        timer.AddCountdownCommand.Execute(null);
        timer.NewName = "  Laundry ";
        timer.AddCountdownCommand.Execute(null);
        timer.NewMinutes = 0;
        timer.NewSeconds = null;
        timer.AddCountdownCommand.Execute(null);

        Assert.Equal(new[] { "2:30 timer", "Laundry" }, timer.Countdowns.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal("Laundry", timer.Countdowns[1].ToString());
        Assert.All(timer.Countdowns, c => Assert.True(c.IsRunning));
        Assert.Equal("", timer.NewName);
        Assert.Equal("2:30", timer.Countdowns[0].RemainingText);
        return Task.CompletedTask;
    });

    [Fact]
    public void Rows_TrackTheService() => OdSta.Run(async () =>
    {
        using var service = new OdTimerService(_time, new OdFakeNotifier());
        using var timer = new OdTimerViewModel(service, _time);
        timer.AddPresetCommand.Execute(1);
        var row = timer.Countdowns.Single();
        Assert.Equal("1 min", timer.Presets[0].Label);
        Assert.Equal("1:00 timer", row.Name);
        Assert.Equal("Pause", row.ToggleText);

        _time.Advance(TimeSpan.FromSeconds(15));
        timer.Refresh();
        Assert.Equal("0:45", row.RemainingText);
        Assert.Equal(0.25, row.Progress, 3);

        timer.ToggleCommand.Execute(row);
        Assert.False(row.IsRunning);
        Assert.Equal("Start", row.ToggleText);
        timer.ToggleCommand.Execute(row);
        _time.Advance(TimeSpan.FromSeconds(45));
        await Task.Yield();
        Assert.True(row.IsFinished);
        Assert.Equal("0:00", row.RemainingText);

        timer.ResetCommand.Execute(row);
        Assert.Equal("1:00", row.RemainingText);
        timer.DeleteCommand.Execute(row);
        Assert.Empty(timer.Countdowns);
        Assert.True(timer.HasNoCountdowns);
    });

    [Fact]
    public void Stopwatch_ShowsElapsedTimeAndLapsNewestFirst() => OdSta.Run(() =>
    {
        using var service = new OdTimerService(_time, new OdFakeNotifier());
        using var timer = new OdTimerViewModel(service, _time);
        Assert.Equal("Start", timer.StopwatchToggleText);

        timer.ToggleStopwatchCommand.Execute(null);
        Assert.Equal("Stop", timer.StopwatchToggleText);
        _time.Advance(TimeSpan.FromSeconds(12.5));
        timer.LapCommand.Execute(null);
        _time.Advance(TimeSpan.FromSeconds(7.25));
        timer.LapCommand.Execute(null);
        timer.ToggleStopwatchCommand.Execute(null);

        Assert.False(timer.StopwatchRunning);
        Assert.Equal("0:19.75", timer.StopwatchText);
        Assert.Equal(new OdLapViewModel("Lap 2", "0:07.25", "0:19.75"), timer.Laps[0]);
        Assert.Equal(new OdLapViewModel("Lap 1", "0:12.50", "0:12.50"), timer.Laps[1]);

        timer.ResetStopwatchCommand.Execute(null);
        Assert.Empty(timer.Laps);
        Assert.Equal("0:00.00", timer.StopwatchText);
        return Task.CompletedTask;
    });

    [Fact]
    public void Ticking_RefreshesWhileVisible_AndDisposeLetsGo() => OdSta.Run(async () =>
    {
        using var service = new OdTimerService(_time, new OdFakeNotifier());
        var timer = new OdTimerViewModel(service, _time);
        service.StartStopwatch();

        timer.StartTicking();
        timer.StartTicking();
        _time.Advance(OdTimerViewModel.RefreshInterval * 3);
        await Task.Yield();
        Assert.Equal("0:00.15", timer.StopwatchText);

        timer.Dispose();
        _time.Advance(TimeSpan.FromSeconds(1));
        service.Lap();
        await Task.Yield();
        Assert.Equal("0:00.15", timer.StopwatchText);
        Assert.Empty(timer.Laps);
    });
}
