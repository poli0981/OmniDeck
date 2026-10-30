using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Shell;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Governor;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Shell;

public sealed class OdMemoryViewModelTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
    private readonly OdFakeMemory _memory = new();
    private readonly OdSettingsService _settings;

    public OdMemoryViewModelTests()
    {
        _settings = new OdSettingsService(new OdDataRoot(_temp.Path), Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Rows_ShowStateAndLastUse() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        using var panel = new OdMemoryViewModel(host, governor, _settings, _time);
        var clock = panel.Rows[0];

        Assert.True(panel.HasModules);
        Assert.Equal("Module_clock", clock.ToString());
        Assert.Equal("Not started", clock.StateText);
        Assert.Equal("", clock.LastUsedText);
        Assert.False(clock.CanClose);

        await host.ShowAsync("clock");
        Assert.Equal("Active", clock.StateText);
        Assert.Equal("On screen", clock.LastUsedText);

        await host.ShowAsync("timer");
        Assert.Equal("Idle", clock.StateText);
        Assert.Equal("Used just now", clock.LastUsedText);
        Assert.True(clock.CanSuspend);
        Assert.True(clock.CanHibernate);
        Assert.True(clock.CanClose);

        _time.Advance(TimeSpan.FromMinutes(4));
        panel.Refresh();
        Assert.Equal("Used 4 min ago", clock.LastUsedText);
        _time.Advance(TimeSpan.FromHours(2));
        panel.Refresh();
        Assert.Equal("Used 2 h ago", clock.LastUsedText);
    });

    [Fact]
    public void Summary_FollowsTheGovernorsPasses() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        using var panel = new OdMemoryViewModel(host, governor, _settings, _time);
        Assert.Equal("In use: " + OdBytes.Format(_memory.Committed), panel.InUseText);
        Assert.Equal($"Soft limit {OdBytes.Format(45 * OdGovernorOptions.GiB / 10)} · hard limit {OdBytes.Format(6 * OdGovernorOptions.GiB)}", panel.LimitsText);

        _memory.Committed = 2 * OdGovernorOptions.GiB;
        await governor.TickAsync();

        Assert.Equal("In use: " + OdBytes.Format(2 * OdGovernorOptions.GiB), panel.InUseText);
    });

    [Fact]
    public void Commands_ActThroughTheGovernor() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        using var panel = new OdMemoryViewModel(host, governor, _settings, _time);
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        await host.ShowAsync("calc");
        var (clock, timer, calc) = (panel.Rows[0], panel.Rows[1], panel.Rows[2]);

        await panel.SuspendCommand.ExecuteAsync(clock);
        await panel.HibernateCommand.ExecuteAsync(timer);
        await panel.CloseCommand.ExecuteAsync(calc);
        panel.ToggleKeepRunningCommand.Execute(clock);

        Assert.Equal("Suspended", clock.StateText);
        Assert.False(clock.CanSuspend);
        Assert.Equal("Hibernated", timer.StateText);
        Assert.False(timer.CanHibernate);
        Assert.Equal("Closed", calc.StateText);
        Assert.True(clock.KeepRunning);
        Assert.Equal(new[] { "clock" }, _settings.Current.Memory.KeepRunning, StringComparer.Ordinal);

        panel.ToggleKeepRunningCommand.Execute(clock);
        Assert.False(clock.KeepRunning);
    });

    [Fact]
    public void Dispose_StopsListening() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        var panel = new OdMemoryViewModel(host, governor, _settings, _time);

        panel.Dispose();
        await host.ShowAsync("clock");

        Assert.Equal("Not started", panel.Rows[0].StateText);
    });

    private OdModuleHost NewHost() => new(
        [new OdFakeModule("clock"), new OdFakeModule("timer"), new OdFakeModule("calc")],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        _time,
        new OdFakeNotifier());

    private OdRamGovernor NewGovernor(OdModuleHost host) => new(host, _settings, _memory, _time, new OdFakeNotifier(), Logger.None);
}
