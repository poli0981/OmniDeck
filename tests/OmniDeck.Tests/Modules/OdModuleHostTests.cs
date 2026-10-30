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
    public void RepeatedIds_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new OdModuleHost(
            [new OdFakeModule("clock"), new OdFakeModule("clock")],
            new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
            Logger.None,
            _time,
            new OdFakeNotifier()));
    }

    [Fact]
    public void Context_GivesEachModuleItsOwnSettingsAndTheNotifier() => OdSta.Run(async () =>
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        var notifier = new OdFakeNotifier();
        using var host = new OdModuleHost([_clock, _timer], database, Logger.None, _time, notifier);
        var zones = (JsonTypeInfo<string[]>)JsonSerializerOptions.Default.GetTypeInfo(typeof(string[]));

        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        _clock.Context!.Settings.Set(["Asia/Ho_Chi_Minh"], zones);
        _clock.Context.Notifier.Notify("Timer", "Done");

        Assert.Equal(new[] { "Asia/Ho_Chi_Minh" }, _clock.Context.Settings.Get(zones));
        Assert.Null(_timer.Context!.Settings.Get(zones));
        Assert.Equal(new[] { ("Timer", "Done") }, notifier.Shown);
    });

    private OdModuleHost NewHost() => new(
        [_clock, _timer],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        _time,
        new OdFakeNotifier());
}
