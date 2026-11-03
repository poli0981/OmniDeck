using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Governor;

public sealed class OdRamGovernorTests : IDisposable
{
    private const long GiB = OdGovernorOptions.GiB;
    private const long MiB = OdGovernorOptions.MiB;

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));
    private readonly OdFakeMemory _memory = new();
    private readonly OdFakeReporter _reporter = new();
    private readonly OdFakeNotifier _notifier = new();
    private readonly OdSettingsService _settings;
    private readonly OdFakeModule _clock = new("clock");
    private readonly OdFakeModule _timer = new("timer");
    private readonly OdFakeModule _web = new("web", OdRamClass.Heavy);
    private readonly OdFakeModule _calc = new("calc");
    private readonly OdFakeModule _mail = new("mail", OdRamClass.Heavy);

    public OdRamGovernorTests()
    {
        _settings = new OdSettingsService(new OdDataRoot(_temp.Path), Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Ladder_SuspendsAfterIdleAfter_ThenHibernatesAfterHibernateAfter() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("calc");

        _time.Advance(TimeSpan.FromMinutes(4));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));
        Assert.Equal(0, _memory.Reclaims);

        _time.Advance(TimeSpan.FromMinutes(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Suspended, State(host, "clock"));
        Assert.Contains("suspend:Hidden", _clock.Calls, StringComparer.Ordinal);
        Assert.Equal(1, _memory.Reclaims);

        _time.Advance(TimeSpan.FromMinutes(25));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "clock"));
        Assert.Null(host.Find("clock")?.View);
        Assert.Equal(OdModuleState.Active, State(host, "calc"));
    });

    [Fact]
    public void Ladder_LeavesKeptRunningAndRefusingModulesAlone() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        await host.ShowAsync("calc");
        governor.SetKeptRunning("clock", keep: true);
        governor.SetKeptRunning("clock", keep: true);
        _timer.CanSuspend = false;

        _time.Advance(TimeSpan.FromHours(1));
        await governor.TickAsync();

        Assert.True(governor.IsKeptRunning("clock"));
        Assert.Equal(new[] { "clock" }, _settings.Current.Memory.KeepRunning, StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));
        Assert.Equal(OdModuleState.Idle, State(host, "timer"));

        governor.SetKeptRunning("clock", keep: false);
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "clock"));
        Assert.Empty(_settings.Current.Memory.KeepRunning);
    });

    [Fact]
    public void SoftCap_HibernatesTheLeastRecentlyUsed_OnePerPass() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        _time.Advance(TimeSpan.FromMinutes(1));
        await host.ShowAsync("timer");
        _time.Advance(TimeSpan.FromMinutes(1));
        await host.ShowAsync("calc");
        _memory.Committed = 5 * GiB;

        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "clock"));
        Assert.Equal(OdModuleState.Idle, State(host, "timer"));

        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "timer"));
        Assert.Equal(OdModuleState.Active, State(host, "calc"));
        Assert.Equal(2, _memory.Reclaims);
        Assert.Empty(_notifier.Shown);
    });

    [Fact]
    public void SoftCap_PrefersHeavyModulesAtEqualRecency() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("web");
        _time.Advance(TimeSpan.FromSeconds(3));
        await host.ShowAsync("calc");
        _memory.Committed = 5 * GiB;

        await governor.TickAsync();

        Assert.Equal(OdModuleState.Hibernated, State(host, "web"));
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));
    });

    [Fact]
    public void HardCap_HibernatesUntilBelow_AndNotifiesOncePerExcursion() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await ShowAllThenCalcAsync(host);
        _memory.Committed = 7 * GiB;
        _memory.FreedPerReclaim = 6 * GiB / 10;

        await governor.TickAsync();
        Assert.Equal(2, host.Modules.Count(m => m.State == OdModuleState.Hibernated));
        Assert.Equal(7 * GiB - (2 * (6 * GiB / 10)), governor.CommittedBytes);
        var (title, message) = Assert.Single(_notifier.Shown);
        Assert.Equal("OmniDeck is using a lot of memory", title);
        Assert.Contains("2 modules", message, StringComparison.Ordinal);

        // Back under the hard cap but over the soft one: one more, quietly.
        await governor.TickAsync();
        Assert.Equal(3, host.Modules.Count(m => m.State == OdModuleState.Hibernated));
        Assert.Single(_notifier.Shown);

        // A new excursion with nothing left to hibernate warns again.
        _memory.Committed = 7 * GiB;
        await governor.TickAsync();
        await governor.TickAsync();
        Assert.Equal(2, _notifier.Shown.Count);
        Assert.Contains("kept running", _notifier.Shown[1].Message, StringComparison.Ordinal);
    });

    [Fact]
    public void Passes_CountTheReportedProcesses_ForTheWholeAndPerModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        _reporter.Shared = 300 * MiB;
        _reporter.Modules["web"] = 200 * MiB;

        await governor.TickAsync();

        Assert.Equal(_memory.Committed + (500 * MiB), governor.CommittedBytes);
        Assert.Equal(200 * MiB, governor.Snapshot?.For("web"));
        Assert.Null(governor.Snapshot?.For("clock"));
        Assert.Equal(300 * MiB, governor.Snapshot?.SharedBytes);

        _reporter.Fails = true;
        await governor.TickAsync();
        Assert.Equal(_memory.Committed, governor.CommittedBytes);
    });

    [Fact]
    public void SoftCap_AtEqualRecency_TakesTheModuleThatUsesMore_AndCountsItGone() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("web");
        await host.ShowAsync("mail");
        await host.ShowAsync("calc");
        _memory.Committed = 4 * GiB;
        _reporter.Shared = 200 * MiB;
        _reporter.Modules["web"] = 300 * MiB;
        _reporter.Modules["mail"] = 600 * MiB;

        await governor.TickAsync();

        Assert.Equal(OdModuleState.Hibernated, State(host, "mail"));
        Assert.Equal(OdModuleState.Idle, State(host, "web"));

        // Its processes take a moment to exit: the pass counts them gone, and the next one measures again.
        Assert.Null(governor.Snapshot?.For("mail"));
        Assert.Equal((4 * GiB) + (500 * MiB), governor.CommittedBytes);
    });

    [Fact]
    public void ResidentModules_SkipTheLadder_GoLastToTheCaps_AndSaySo() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        _web.StaysResident = true;
        await host.ShowAsync("web");
        _time.Advance(TimeSpan.FromMinutes(1));
        await host.ShowAsync("timer");
        _time.Advance(TimeSpan.FromMinutes(1));
        await host.ShowAsync("calc");

        _time.Advance(TimeSpan.FromHours(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "web"));
        Assert.Equal(OdModuleState.Hibernated, State(host, "timer"));

        await host.ShowAsync("timer");
        await host.ShowAsync("calc");
        _memory.Committed = 5 * GiB;
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "timer"));
        Assert.Equal(OdModuleState.Idle, State(host, "web"));
        Assert.Empty(_notifier.Raised);

        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "web"));
        var told = Assert.Single(_notifier.Raised);
        Assert.Equal("Module_web was hibernated to save memory", told.Title);
        Assert.Equal("web", told.Target);
    });

    [Fact]
    public void HardCap_HeldThereBySharedProcesses_WarnsInsteadOfHibernating() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("web");
        _reporter.Shared = 6 * GiB;
        _reporter.Modules["web"] = 400 * MiB;

        await governor.TickAsync();

        Assert.Equal(OdModuleState.Idle, State(host, "clock"));
        var (title, message) = Assert.Single(_notifier.Shown);
        Assert.Equal("OmniDeck is using a lot of memory", title);
        Assert.Contains("the open web apps share use " + OdBytes.Format(6 * GiB), message, StringComparison.Ordinal);
    });

    [Fact]
    public void HardCap_HibernatingTheLastWebApp_CountsTheSharedProcessesGone() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("web");
        await host.ShowAsync("calc");
        _reporter.Shared = 6 * GiB;
        _reporter.Modules["web"] = 400 * MiB;

        await governor.TickAsync();

        Assert.Equal(OdModuleState.Hibernated, State(host, "web"));
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));
        Assert.Equal(_memory.Committed, governor.CommittedBytes);
        Assert.Contains("hibernated a module", Assert.Single(_notifier.Shown).Message, StringComparison.Ordinal);
    });

    [Fact]
    public void Sampled_ReportsTheMemoryAndTheLimits() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        OdGovernorSampledEventArgs? sampled = null;
        governor.Sampled += (_, e) => sampled = e;
        Assert.Equal(_memory.Committed, governor.CommittedBytes);

        await governor.TickAsync();
        _memory.Committed += GiB;

        Assert.Equal(_memory.Committed - GiB, sampled?.CommittedBytes);
        Assert.Equal(45 * GiB / 10, sampled?.Options.SoftCapBytes);
        Assert.Equal(6 * GiB, sampled?.Options.HardCapBytes);
        Assert.Equal(_memory.Committed - GiB, governor.CommittedBytes);
    });

    [Fact]
    public void PanelActions_GoThroughTheHost_AndReclaim() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await ShowAllThenCalcAsync(host);

        Assert.True(await governor.SuspendAsync("clock"));
        Assert.True(await governor.HibernateAsync("timer"));
        Assert.False(await governor.HibernateAsync("timer"));
        await governor.CloseAsync("web");

        Assert.Contains("suspend:User", _clock.Calls, StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Hibernated, State(host, "timer"));
        Assert.Equal(OdModuleState.Closed, State(host, "web"));
        Assert.Equal(2, _memory.Reclaims);
    });

    [Fact]
    public void Start_PollsOnTheUiContext_AndSurvivesAFailingModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await ShowAllThenCalcAsync(host);
        _clock.ThrowOnHibernate = true;
        var passes = 0;
        governor.Sampled += (_, _) => passes++;

        governor.Start();
        _time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(50);
        Assert.Equal(0, passes);
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));

        _clock.ThrowOnHibernate = false;
        _time.Advance(OdGovernorOptions.DefaultPollInterval);
        await Task.Delay(50);
        Assert.Equal(1, passes);
        Assert.Equal(OdModuleState.Hibernated, State(host, "clock"));
    });

    [Fact]
    public void Start_NeedsTheUiThread()
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(governor.Start));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
    }

    [Fact]
    public void Ladder_TakesTheMainWindowsModule_OnlyWhileTheWindowIsAway() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");

        _time.Advance(TimeSpan.FromHours(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Active, State(host, "clock"));

        await host.SetMainWindowAwayAsync(away: true);
        _time.Advance(TimeSpan.FromMinutes(5));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Suspended, State(host, "clock"));

        _time.Advance(TimeSpan.FromMinutes(25));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "clock"));

        Assert.NotNull(await host.SetMainWindowAwayAsync(away: false));
        Assert.Equal(OdModuleState.Active, State(host, "clock"));
    });

    [Fact]
    public void SoftCap_TakesTheMainWindowsModule_OnlyWhileTheWindowIsAway() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("web");
        _memory.Committed = 5 * GiB;

        await governor.TickAsync();
        Assert.Equal(OdModuleState.Active, State(host, "web"));

        await host.SetMainWindowAwayAsync(away: true);
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Hibernated, State(host, "web"));
    });

    [Fact]
    public void Ladder_CountsHiddenTimeFromTheEndOfAPin() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        await host.ShowAsync("clock");
        await host.ShowAsync("calc");
        _clock.CanSuspend = false;

        _time.Advance(TimeSpan.FromMinutes(40));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));

        // It stopped playing: hidden for 40 minutes, but in use until now.
        _clock.CanSuspend = true;
        _time.Advance(TimeSpan.FromMinutes(4));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));

        _time.Advance(TimeSpan.FromMinutes(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Suspended, State(host, "clock"));
    });

    [Fact]
    public void KeptRunningAndResidentModules_StayUp_WhileTheWindowIsAway() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var governor = NewGovernor(host);
        governor.SetKeptRunning("clock", keep: true);
        _web.StaysResident = true;

        await host.ShowAsync("clock");
        await host.SetMainWindowAwayAsync(away: true);
        _time.Advance(TimeSpan.FromHours(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "clock"));

        await host.SetMainWindowAwayAsync(away: false);
        await host.ShowAsync("web");
        await host.SetMainWindowAwayAsync(away: true);
        _time.Advance(TimeSpan.FromHours(1));
        await governor.TickAsync();
        Assert.Equal(OdModuleState.Idle, State(host, "web"));
    });

    private static OdModuleState? State(OdModuleHost host, string id) => host.Find(id)?.State;

    /// <summary>Opens clock, timer and web one after another, then calc, so the first three are hidden.</summary>
    private static async Task ShowAllThenCalcAsync(OdModuleHost host)
    {
        await host.ShowAsync("clock");
        await host.ShowAsync("timer");
        await host.ShowAsync("web");
        await host.ShowAsync("calc");
    }

    private OdModuleHost NewHost() => new(
        [_clock, _timer, _web, _calc, _mail],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        _time,
        _notifier,
        OdTestHttp.Client);

    private OdRamGovernor NewGovernor(OdModuleHost host) => new(host, _settings, _memory, [_reporter], _time, _notifier, Logger.None);
}
