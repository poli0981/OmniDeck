using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Modules;

public sealed class OdModuleHostTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly OdFakeModule _clock = new("clock");
    private readonly OdFakeModule _timer = new("timer");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Show_InitializesOnce_BuildsTheView_AndActivates() => OdSta.Run(async () =>
    {
        using var host = NewHost();

        var view = await host.ShowAsync("clock");
        var again = await host.ShowAsync("clock");

        Assert.Same(view, again);
        Assert.Equal(new[] { "init", "view", "activate" }, _clock.Calls, StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.Same(host.Find("clock"), host.Current);
        Assert.Equal("clock", _clock.Context?.ModuleId);
        Assert.Same(_time, _clock.Context?.Time);
    });

    [Fact]
    public void Show_HidesThePreviousModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var changes = new List<string>();
        host.StateChanged += (_, e) => changes.Add($"{e.Module.Info.Id}:{e.Previous}>{e.Module.State}");

        await host.ShowAsync("clock");
        _time.Advance(TimeSpan.FromMinutes(3));
        await host.ShowAsync("timer");

        var clock = host.Find("clock")!;
        Assert.Equal(OdModuleState.Idle, clock.State);
        Assert.Equal(Start.AddMinutes(3), clock.LastActive);
        Assert.NotNull(clock.View);
        Assert.Same(host.Find("timer"), host.Current);
        Assert.Equal(new[] { "clock:Created>Active", "clock:Active>Idle", "timer:Created>Active" }, changes, StringComparer.Ordinal);
    });

    [Fact]
    public void Hide_LeavesNoModuleVisible() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");

        await host.HideAsync();
        await host.HideAsync();

        Assert.Null(host.Current);
        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
    });

    [Fact]
    public void Suspend_OnlyMovesHiddenModules() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");

        Assert.False(await host.SuspendAsync("clock", OdSuspendReason.Hidden));
        Assert.False(await host.SuspendAsync("timer", OdSuspendReason.Hidden));

        await host.ShowAsync("timer");
        Assert.True(await host.SuspendAsync("clock", OdSuspendReason.Hidden));
        Assert.False(await host.SuspendAsync("clock", OdSuspendReason.MemoryPressure));

        Assert.Equal(OdModuleState.Suspended, host.Find("clock")?.State);
        Assert.Equal(new[] { "init", "view", "activate", "suspend:Hidden" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void PinnedModules_AreNeitherSuspendedNorHibernated() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        _clock.CanSuspend = false;

        Assert.False(await host.SuspendAsync("clock", OdSuspendReason.MemoryPressure));
        Assert.False(await host.HibernateAsync("clock"));

        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
        Assert.NotNull(host.Find("clock")?.View);
    });

    [Fact]
    public void Hibernate_DropsTheView_AndShowRebuildsIt() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var first = await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        await host.SuspendAsync("clock", OdSuspendReason.Hidden);

        Assert.True(await host.HibernateAsync("clock"));
        Assert.Null(host.Find("clock")?.View);
        Assert.Equal(OdModuleState.Hibernated, host.Find("clock")?.State);
        Assert.False(await host.HibernateAsync("clock"));

        var second = await host.ShowAsync("clock");
        Assert.NotSame(first, second);
        Assert.Equal(new[] { "init", "view", "activate", "suspend:Hidden", "hibernate", "view", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void Close_ReleasesTheVisibleModule_AndReopeningSkipsInitialization() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        _time.Advance(TimeSpan.FromSeconds(30));

        await host.CloseAsync("clock");
        await host.CloseAsync("clock");
        await host.CloseAsync("timer");

        var clock = host.Find("clock")!;
        Assert.Null(host.Current);
        Assert.Null(clock.View);
        Assert.Equal(OdModuleState.Closed, clock.State);
        Assert.Equal(Start.AddSeconds(30), clock.LastActive);
        Assert.Empty(_timer.Calls);

        await host.ShowAsync("clock");
        Assert.Equal(new[] { "init", "view", "activate", "close", "view", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void UnknownModules_AreRejected() => OdSta.Run(async () =>
    {
        using var host = NewHost();

        await Assert.ThrowsAsync<ArgumentException>(() => host.ShowAsync("nope"));
        Assert.Null(host.Find("nope"));
        Assert.Equal(new[] { "clock", "timer" }, host.Modules.Select(m => m.Info.Id), StringComparer.Ordinal);
    });

    [Fact]
    public void Show_LeavesThePreviousModuleShown_WhenTheNewOneFailsToActivate() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        _timer.ThrowOnActivate = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ShowAsync("timer"));

        Assert.Same(host.Find("clock"), host.Current);
        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.NotEqual(OdModuleState.Active, host.Find("timer")?.State);
    });

    [Fact]
    public void CloseAll_ClosesEveryOpenModule_EvenWhenOneFails() => OdSta.Run(async () =>
    {
        var notes = new OdFakeModule("notes");
        using var host = NewHost(notes);
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        _clock.ThrowOnClose = true;

        await host.CloseAllAsync();

        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
        Assert.Equal(OdModuleState.Closed, host.Find("timer")?.State);
        Assert.Equal(OdModuleState.Created, host.Find("notes")?.State);
        Assert.Null(host.Current);
        Assert.DoesNotContain("close", notes.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void ARepeatedId_KeepsTheFirstModule()
    {
        var first = new OdFakeModule("clock");
        using var host = new OdModuleHost(
            [first, new OdFakeModule("clock")],
            new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
            Logger.None,
            _time,
            new OdFakeNotifier(),
            OdTestHttp.Client);

        Assert.Same(first, Assert.Single(host.Modules).Module);
    }

    [Fact]
    public void Context_GivesEachModuleItsOwnSettingsAndTheNotifier() => OdSta.Run(async () =>
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        var notifier = new OdFakeNotifier();
        using var host = new OdModuleHost([_clock, _timer], database, Logger.None, _time, notifier, OdTestHttp.Client);
        var zones = (JsonTypeInfo<string[]>)JsonSerializerOptions.Default.GetTypeInfo(typeof(string[]));

        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        _clock.Context!.Settings.Set(["Asia/Ho_Chi_Minh"], zones);
        _clock.Context.Notifier.Notify("Timer", "Done");
        _clock.Context.Notifier.Notify(new OdNotification("Alarm", "Elsewhere", "timer"));

        Assert.Equal(new[] { "Asia/Ho_Chi_Minh" }, _clock.Context.Settings.Get(zones));
        Assert.Null(_timer.Context!.Settings.Get(zones));
        Assert.Equal(new[] { new OdNotification("Timer", "Done", "clock"), new OdNotification("Alarm", "Elsewhere", "timer") }, notifier.Raised);
        Assert.Same(OdTestHttp.Client, _clock.Context.Http);
        Assert.Same(database, _clock.Context.Database);
    });

    [Fact]
    public void PopOut_KeepsTheModuleActive_BesideTheMainWindowsModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");

        var view = await host.PopOutAsync("timer");

        var timer = host.Find("timer")!;
        Assert.Same(timer.View, view);
        Assert.True(timer.IsPoppedOut);
        Assert.Equal(OdModuleState.Active, timer.State);
        Assert.Same(host.Find("clock"), host.Current);
        Assert.Equal(new[] { "init", "view", "activate" }, _timer.Calls, StringComparer.Ordinal);
        Assert.False(await host.SuspendAsync("timer", OdSuspendReason.MemoryPressure));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ShowAsync("timer"));
    });

    [Fact]
    public void PopOut_OfTheMainWindowsModule_LeavesTheMainWindowEmpty() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var shown = await host.ShowAsync("clock");

        var view = await host.PopOutAsync("clock");

        Assert.Same(shown, view);
        Assert.Null(host.Current);
        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.Equal(new[] { "init", "view", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void Dock_HidesTheModule_AndTheLadderAppliesAgain() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var view = await host.PopOutAsync("clock");
        _time.Advance(TimeSpan.FromMinutes(5));

        await host.DockAsync("clock");
        await host.DockAsync("clock");

        var clock = host.Find("clock")!;
        Assert.False(clock.IsPoppedOut);
        Assert.Equal(OdModuleState.Idle, clock.State);
        Assert.Equal(Start.AddMinutes(5), clock.LastActive);
        Assert.True(await host.SuspendAsync("clock", OdSuspendReason.Hidden));
        Assert.Same(view, await host.ShowAsync("clock"));
    });

    [Fact]
    public void Close_EndsAPopOut() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.PopOutAsync("clock");
        _time.Advance(TimeSpan.FromMinutes(2));

        await host.CloseAsync("clock");

        var clock = host.Find("clock")!;
        Assert.False(clock.IsPoppedOut);
        Assert.Null(clock.View);
        Assert.Equal(OdModuleState.Closed, clock.State);
        Assert.Equal(Start.AddMinutes(2), clock.LastActive);
    });

    [Fact]
    public void WindowAway_HidesItsModule_ButKeepsItCurrent() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var view = await host.ShowAsync("clock");
        var changes = new List<string>();
        host.StateChanged += (_, e) => changes.Add($"{e.Module.Info.Id}:{e.Previous}>{e.Module.State}");
        _time.Advance(TimeSpan.FromMinutes(2));

        Assert.Null(await host.SetMainWindowAwayAsync(away: true));
        Assert.Null(await host.SetMainWindowAwayAsync(away: true));

        var clock = host.Find("clock")!;
        Assert.True(host.IsMainWindowAway);
        Assert.Same(clock, host.Current);
        Assert.Equal(OdModuleState.Idle, clock.State);
        Assert.Equal(Start.AddMinutes(2), clock.LastActive);
        Assert.True(await host.SuspendAsync("clock", OdSuspendReason.Hidden));

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Same(view, await host.SetMainWindowAwayAsync(away: false));

        Assert.False(host.IsMainWindowAway);
        Assert.Equal(OdModuleState.Active, clock.State);
        Assert.Equal(Start.AddMinutes(3), clock.LastActive);
        Assert.Equal(new[] { "clock:Active>Idle", "clock:Idle>Suspended", "clock:Suspended>Active" }, changes, StringComparer.Ordinal);
        Assert.Equal(new[] { "init", "view", "activate", "suspend:Hidden", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void WindowBack_AfterAHibernation_BuildsANewView() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var first = await host.ShowAsync("clock");
        await host.SetMainWindowAwayAsync(away: true);
        Assert.True(await host.HibernateAsync("clock"));

        var second = await host.SetMainWindowAwayAsync(away: false);

        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Same(second, host.Find("clock")?.View);
        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.Equal(new[] { "init", "view", "activate", "hibernate", "view", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void WindowAway_OnAShellPage_ChangesNoModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        await host.HideAsync();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(await host.SetMainWindowAwayAsync(away: true));
        Assert.Null(await host.SetMainWindowAwayAsync(away: false));

        Assert.Null(host.Current);
        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
        Assert.Equal(Start, host.Find("clock")?.LastActive);
    });

    [Fact]
    public void ShowingAnotherModule_WhileAway_LeavesTheFirstOneWhereTheLadderHasIt() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        await host.SetMainWindowAwayAsync(away: true);
        _time.Advance(TimeSpan.FromMinutes(6));
        await host.SuspendAsync("clock", OdSuspendReason.Hidden);

        // A notification for Timer, clicked while the window is in the tray.
        await host.ShowAsync("timer");

        Assert.Equal(OdModuleState.Suspended, host.Find("clock")?.State);
        Assert.Equal(Start, host.Find("clock")?.LastActive);
        Assert.Same(host.Find("timer"), host.Current);
        Assert.Equal(OdModuleState.Active, host.Find("timer")?.State);
    });

    [Fact]
    public void ShowingTheWindowsOwnModule_WhileAway_ActivatesItAgain() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        var view = await host.ShowAsync("clock");
        await host.SetMainWindowAwayAsync(away: true);
        await host.SuspendAsync("clock", OdSuspendReason.Hidden);

        Assert.Same(view, await host.ShowAsync("clock"));

        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.Equal(new[] { "init", "view", "activate", "suspend:Hidden", "activate" }, _clock.Calls, StringComparer.Ordinal);
    });

    [Fact]
    public void APopOut_StaysActive_WhileTheMainWindowIsAway() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        await host.PopOutAsync("timer");

        await host.SetMainWindowAwayAsync(away: true);

        Assert.Equal(OdModuleState.Active, host.Find("timer")?.State);
        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
    });

    [Fact]
    public void IsPinned_RestartsTheHiddenTime_OfAModuleThatRefusesToSuspend() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        _clock.CanSuspend = false;
        _time.Advance(TimeSpan.FromMinutes(40));

        Assert.True(await host.IsPinnedAsync("clock"));
        Assert.Equal(Start.AddMinutes(40), host.Find("clock")?.LastActive);

        _clock.CanSuspend = true;
        Assert.False(await host.IsPinnedAsync("clock"));
        Assert.False(await host.IsPinnedAsync("timer"));
    });

    [Fact]
    public void AModuleLeavingTheScreen_IsToldSo_Once() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");

        await host.ShowAsync("timer");
        Assert.Equal(1, _clock.Deactivations);

        await host.SetMainWindowAwayAsync(away: true);
        await host.SetMainWindowAwayAsync(away: true);
        Assert.Equal(1, _timer.Deactivations);

        await host.SetMainWindowAwayAsync(away: false);
        await host.HideAsync();
        Assert.Equal(2, _timer.Deactivations);
        await host.HideAsync();
        Assert.Equal(2, _timer.Deactivations);

        await host.PopOutAsync("clock");
        await host.DockAsync("clock");
        Assert.Equal(2, _clock.Deactivations);
    });

    [Fact]
    public void AModuleThatFailsToDeactivate_IsHiddenAllTheSame() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        await host.ShowAsync("clock");
        _clock.ThrowOnDeactivate = true;

        await host.ShowAsync("timer");

        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
        Assert.Same(host.Find("timer"), host.Current);
    });

    private OdModuleHost NewHost(params IOdModule[] more) => new(
        [_clock, _timer, .. more],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        _time,
        new OdFakeNotifier(),
        OdTestHttp.Client);
}
