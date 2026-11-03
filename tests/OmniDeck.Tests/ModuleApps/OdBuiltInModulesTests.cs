using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Timer;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.ModuleApps;

[Collection(OdCultureCollection.Name)]
public sealed class OdBuiltInModulesTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void EveryModule_GoesDownTheLadderAndComesBack() => OdSta.Run(async () =>
    {
        using var timers = new OdTimerService(_time, new OdFakeNotifier());
        using var host = NewHost(new OdClockModule(), new OdTimerModule(timers), new OdCalculatorModule());

        foreach (var hosted in host.Modules)
        {
            var id = hosted.Info.Id;
            var first = await host.ShowAsync(id);
            await host.HideAsync();
            Assert.True(await hosted.Module.CanSuspendAsync());
            Assert.True(await host.SuspendAsync(id, OdSuspendReason.Hidden));
            Assert.True(await host.HibernateAsync(id));

            var second = await host.ShowAsync(id);
            Assert.NotSame(first, second);
            await host.CloseAsync(id);
            Assert.Equal(OdModuleState.Closed, hosted.State);
        }

        Assert.Equal(new[] { "clock", "timer", "calculator" }, host.Modules.Select(m => m.Info.Id), StringComparer.Ordinal);
        Assert.Equal(new[] { "Clock24", "Timer24", "Calculator24" }, host.Modules.Select(m => m.Info.Icon), StringComparer.Ordinal);
    });

    [Fact]
    public void TimersKeepRunning_WhileTheTimerModuleIsHibernated() => OdSta.Run(async () =>
    {
        var notifier = new OdFakeNotifier();
        using var timers = new OdTimerService(_time, notifier);
        using var host = NewHost(new OdTimerModule(timers), new OdCalculatorModule());
        await host.ShowAsync("timer");
        timers.Start(timers.Add("Tea", TimeSpan.FromMinutes(3)));
        await host.ShowAsync("calculator");
        await host.HibernateAsync("timer");

        _time.Advance(TimeSpan.FromMinutes(3));
        await Task.Yield();

        Assert.Single(notifier.Shown);
    });

    [Fact]
    public void ClockZonesAndCalculatorModes_SurviveHibernation() => OdSta.Run(async () =>
    {
        using var host = NewHost(new OdClockModule(), new OdCalculatorModule());

        var clockView = await host.ShowAsync("clock");
        var clock = (OdClockViewModel)clockView.DataContext;
        clock.SelectedZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        clock.AddZoneCommand.Execute(null);

        var calculatorView = await host.ShowAsync("calculator");
        var calculator = (OdCalculatorViewModel)calculatorView.DataContext;
        calculator.IsScientific = true;
        calculator.InputCommand.Execute("7");
        calculator.EvaluateCommand.Execute(null);

        await host.HibernateAsync("clock");
        var clockAgain = (OdClockViewModel)(await host.ShowAsync("clock")).DataContext;
        await host.HibernateAsync("calculator");
        var calculatorAgain = (OdCalculatorViewModel)(await host.ShowAsync("calculator")).DataContext;

        Assert.Equal("Tokyo Standard Time", clockAgain.Clocks[1].Zone.Id);
        Assert.True(calculatorAgain.IsScientific);
        Assert.Single(calculatorAgain.History);

        await host.CloseAsync("calculator");
        Assert.Empty(calculatorAgain.History);
    });

    [Fact]
    public void ClockAndTimer_TickAgain_WhenTheWindowComesBackAfterASuspension() => OdSta.Run(async () =>
    {
        using var timers = new OdTimerService(_time, new OdFakeNotifier());
        using var host = NewHost(new OdClockModule(), new OdTimerModule(timers));

        // A minimized window never unloads its view, so only activation can restart the ticking.
        var clock = (OdClockViewModel)(await host.ShowAsync("clock")).DataContext;
        await host.SetMainWindowAwayAsync(away: true);
        Assert.True(await host.SuspendAsync("clock", OdSuspendReason.Hidden));
        var clockShown = clock.Clocks[0].TimeText;
        _time.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);
        Assert.Equal(clockShown, clock.Clocks[0].TimeText);
        await host.SetMainWindowAwayAsync(away: false);
        Assert.NotEqual(clockShown, clock.Clocks[0].TimeText, StringComparer.Ordinal);

        var timer = (OdTimerViewModel)(await host.ShowAsync("timer")).DataContext;
        timer.ToggleStopwatchCommand.Execute(null);
        await host.SetMainWindowAwayAsync(away: true);
        Assert.True(await host.SuspendAsync("timer", OdSuspendReason.Hidden));
        var timerShown = timer.StopwatchText;
        _time.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(50);
        Assert.Equal(timerShown, timer.StopwatchText);
        await host.SetMainWindowAwayAsync(away: false);
        Assert.NotEqual(timerShown, timer.StopwatchText, StringComparer.Ordinal);
    });

    private OdModuleHost NewHost(params IOdModule[] modules)
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        return new OdModuleHost(modules, database, Logger.None, _time, new OdFakeNotifier(), OdTestHttp.Client);
    }
}
