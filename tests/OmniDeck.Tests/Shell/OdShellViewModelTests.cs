using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Shell;

public sealed class OdShellViewModelTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly OdSettingsService _settings;
    private readonly OdFakeLifetime _lifetime = new();

    public OdShellViewModelTests()
    {
        _settings = new OdSettingsService(new OdDataRoot(_temp.Path), Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Library_ListsEveryModuleInRegistrationOrder() => OdSta.Run(() =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);

        Assert.True(shell.HasModules);
        Assert.Equal(new[] { "clock", "timer", "calc" }, shell.Library.Select(item => item.Id), StringComparer.Ordinal);
        Assert.Equal("Module_clock", shell.Library[0].Title);
        Assert.Equal("Test", shell.Library[0].Category);
        Assert.Empty(shell.Sidebar);
        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        return Task.CompletedTask;
    });

    [Fact]
    public void Navigate_ShowsTheModule_AndListsItWhileOpen() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);

        await shell.NavigateAsync("timer");
        await shell.NavigateAsync("clock");

        Assert.Equal("clock", shell.CurrentTarget);
        Assert.Same(host.Find("clock")?.View, shell.CurrentView);
        Assert.Equal(new[] { "timer", "clock" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Idle, shell.Library[1].State);
        Assert.True(shell.Library[1].IsOpen);

        await shell.NavigateAsync(OdShellViewModel.LibraryTarget);
        await shell.NavigateAsync("not-a-module");

        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        Assert.Null(shell.CurrentView);
        Assert.Null(host.Current);
        Assert.Equal(2, shell.Sidebar.Count);
    });

    [Fact]
    public void ACappedSidebar_KeepsTheModuleOnScreenInView() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("clock");
        await shell.NavigateAsync("timer");
        await shell.NavigateAsync("calc");

        Assert.Equal(new[] { "clock", "timer", "calc" }, shell.VisibleSidebar(0).Select(item => item.Id), StringComparer.Ordinal);
        Assert.Equal(new[] { "clock", "calc" }, shell.VisibleSidebar(2).Select(item => item.Id), StringComparer.Ordinal);

        await shell.NavigateAsync(OdShellViewModel.LibraryTarget);
        Assert.Equal(new[] { "clock", "timer" }, shell.VisibleSidebar(2).Select(item => item.Id), StringComparer.Ordinal);
    });

    [Fact]
    public void Shortcuts_OpenSidebarModulesAndSettings() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("timer");
        await shell.NavigateAsync("calc");

        Assert.Equal("timer", shell.ShortcutTarget(Key.D1, 0));
        Assert.Equal("calc", shell.ShortcutTarget(Key.NumPad2, 0));
        Assert.Null(shell.ShortcutTarget(Key.D3, 0));
        Assert.Equal(OdShellViewModel.SettingsTarget, shell.ShortcutTarget(Key.OemComma, 0));
        Assert.Null(shell.ShortcutTarget(Key.A, 0));
        Assert.Equal("calc", shell.ShortcutTarget(Key.D1, 1));
    });

    [Fact]
    public void Pins_ComeFirstInPinOrder_AndUnknownPinsAreSkipped() => OdSta.Run(async () =>
    {
        _settings.Update(s => s with { PinnedModules = ["calc", "web-app-gone", "clock"] });
        using var host = NewHost();
        using var shell = NewShell(host);

        await shell.NavigateAsync("timer");

        Assert.Equal(new[] { "calc", "clock", "timer" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);
        Assert.True(shell.Library[0].IsPinned);
        Assert.False(shell.Library[1].IsPinned);
    });

    [Fact]
    public void TogglePin_PinsAndUnpinsThroughTheSettings() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var clock = shell.Library[0];
        var timer = shell.Library[1];

        shell.TogglePinCommand.Execute(clock);
        shell.TogglePinCommand.Execute(timer);
        Assert.Equal(new[] { "clock", "timer" }, _settings.Current.PinnedModules, StringComparer.Ordinal);
        Assert.Equal(new[] { "clock", "timer" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);

        await shell.NavigateAsync("clock");
        shell.TogglePinCommand.Execute(clock);
        shell.TogglePinCommand.Execute(timer);

        // Unpinned but open, the clock keeps its place like a tab; the timer was never opened.
        Assert.Empty(_settings.Current.PinnedModules);
        Assert.Equal(new[] { "clock" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);
    });

    [Fact]
    public void ChangingOnlyAPin_StillRebuildsTheSidebar() => OdSta.Run(async () =>
    {
        _settings.Update(s => s with { PinnedModules = ["clock"] });
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("clock");
        await shell.NavigateAsync("timer");
        var rebuilds = 0;
        shell.Sidebar.CollectionChanged += (_, e) => rebuilds += e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ? 1 : 0;

        // Same order either way (clock, then timer); only the pinned group changes.
        shell.TogglePinCommand.Execute(shell.Library[0]);

        Assert.Equal(1, rebuilds);
        Assert.Equal(new[] { "clock", "timer" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);
    });

    [Fact]
    public void Close_RemovesAnUnpinnedModule_AndShowsTheLibraryInstead() => OdSta.Run(async () =>
    {
        _settings.Update(s => s with { PinnedModules = ["timer"] });
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("timer");
        await shell.NavigateAsync("clock");

        await shell.CloseCommand.ExecuteAsync(shell.Library[0]);
        await shell.CloseCommand.ExecuteAsync(shell.Library[1]);

        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        Assert.Null(shell.CurrentView);
        Assert.Equal(new[] { "timer" }, shell.Sidebar.Select(item => item.Id), StringComparer.Ordinal);
        Assert.Equal(OdModuleState.Closed, shell.Library[1].State);
        Assert.False(shell.Library[1].IsOpen);
    });

    [Fact]
    public void Open_NavigatesToTheModule() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);

        await shell.OpenCommand.ExecuteAsync(shell.Library[2]);

        Assert.Equal("calc", shell.CurrentTarget);
        Assert.Equal(OdModuleState.Active, shell.Library[2].State);
    });

    [Fact]
    public void PopOut_OfTheModuleOnScreen_LeavesTheLibraryInTheMainWindow() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var opened = new List<OdPopOutEventArgs>();
        shell.PopOutOpened += (_, e) => opened.Add(e);
        await shell.NavigateAsync("clock");

        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);

        var popOut = Assert.Single(opened);
        Assert.Same(shell.Library[0], popOut.Item);
        Assert.Same(host.Find("clock")?.View, popOut.View);
        Assert.True(shell.Library[0].IsPoppedOut);
        Assert.Equal(OdModuleState.Active, shell.Library[0].State);
        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        Assert.Null(shell.CurrentView);
        Assert.Contains(shell.Library[0], shell.Sidebar);
    });

    [Fact]
    public void APoppedOutModule_IsBroughtUpInItsWindow() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var raised = new List<string>();
        shell.PopOutActivationRequested += (_, e) => raised.Add(e.Item.Id);
        await shell.NavigateAsync("timer");
        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);

        await shell.NavigateAsync("clock");
        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);

        Assert.Equal(new[] { "clock", "clock" }, raised, StringComparer.Ordinal);
        Assert.Equal("timer", shell.CurrentTarget);
        Assert.Same(host.Find("timer"), host.Current);
    });

    [Fact]
    public void PopIn_ClosesTheWindow_AndShowsTheModuleInTheMainWindow() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var closed = new List<string>();
        shell.PopOutCloseRequested += (_, e) => closed.Add(e.Item.Id);
        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);
        await shell.PopInCommand.ExecuteAsync(shell.Library[1]);

        await shell.PopInCommand.ExecuteAsync(shell.Library[0]);

        Assert.Equal(new[] { "clock" }, closed, StringComparer.Ordinal);
        Assert.False(shell.Library[0].IsPoppedOut);
        Assert.Equal("clock", shell.CurrentTarget);
        Assert.Same(host.Find("clock")?.View, shell.CurrentView);
        Assert.Same(host.Find("clock"), host.Current);
    });

    [Fact]
    public void ClosingItsWindow_DocksTheModuleHidden() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("timer");
        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);

        await shell.DockAsync(shell.Library[0]);

        Assert.False(shell.Library[0].IsPoppedOut);
        Assert.Equal(OdModuleState.Idle, shell.Library[0].State);
        Assert.Equal("timer", shell.CurrentTarget);
        Assert.Contains(shell.Library[0], shell.Sidebar);
    });

    [Fact]
    public void ClosingAPoppedOutModule_ClosesItsWindow() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var closed = new List<string>();
        shell.PopOutCloseRequested += (_, e) => closed.Add(e.Item.Id);
        await shell.PopOutCommand.ExecuteAsync(shell.Library[0]);

        await shell.CloseCommand.ExecuteAsync(shell.Library[0]);

        Assert.Equal(new[] { "clock" }, closed, StringComparer.Ordinal);
        Assert.False(shell.Library[0].IsPoppedOut);
        Assert.DoesNotContain(shell.Library[0], shell.Sidebar);
        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
    });

    [Fact]
    public void PaletteEntries_OpenModules_TheLibrary_EachSettingsTab_AndTheShellsCommands() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);

        var entries = shell.PaletteEntries();
        var requested = new List<OdSettingsSection>();
        var commands = new List<OdShellCommand>();
        shell.SettingsSectionRequested += (_, e) => requested.Add(e.Section);
        shell.CommandRequested += (_, e) => commands.Add(e.Command);

        Assert.Equal(
            new[] { "Module_clock", "Module_timer", "Module_calc", "Module library", "Settings", "General", "Appearance", "Memory", "Web apps", "Data", "About", "Running modules", "Check for updates", "Report a bug", "Open the data folder", "Quit OmniDeck" },
            entries.Select(e => e.Title),
            StringComparer.Ordinal);
        Assert.Equal("Command", entries[^1].Detail);
        Assert.Equal("Settings", entries[6].Detail);
        await entries[1].RunAsync();
        Assert.Equal("timer", shell.CurrentTarget);
        await entries[4].RunAsync();
        Assert.Equal(OdShellViewModel.SettingsTarget, shell.CurrentTarget);
        Assert.Null(host.Current);
        await entries[3].RunAsync();
        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        await entries[6].RunAsync();
        Assert.Equal(OdShellViewModel.SettingsTarget, shell.CurrentTarget);
        await entries[11].RunAsync();
        await entries[12].RunAsync();
        await entries[13].RunAsync();
        await entries[14].RunAsync();
        Assert.Equal(new[] { OdSettingsSection.Appearance, OdSettingsSection.Memory, OdSettingsSection.About }, requested);
        Assert.Equal(new[] { OdShellCommand.CheckForUpdates, OdShellCommand.ReportBug, OdShellCommand.OpenDataFolder }, commands);
        Assert.False(_lifetime.IsQuitting);
        await entries[15].RunAsync();
        Assert.True(_lifetime.IsQuitting);
    });

    [Fact]
    public void RefreshTexts_AnnouncesTheLocalizedProperties() => OdSta.Run(() =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)shell.Library[0]).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.Library[0].RefreshTexts();

        Assert.Equal(new[] { "Title", "Category" }, changed, StringComparer.Ordinal);
        return Task.CompletedTask;
    });

    [Fact]
    public void Dispose_StopsFollowingTheSettings() => OdSta.Run(() =>
    {
        using var host = NewHost();
        var shell = NewShell(host);

        shell.Dispose();
        _settings.Update(s => s with { PinnedModules = ["clock"] });

        Assert.Empty(shell.Sidebar);
        return Task.CompletedTask;
    });

    [Fact]
    public void WindowAway_HidesTheModule_AndBackShowsItAgain_OnTheSameView() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("clock");
        var view = shell.CurrentView;
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await shell.SetWindowAwayAsync(away: true);
        await shell.SetWindowAwayAsync(away: true);
        Assert.Equal(OdModuleState.Idle, host.Find("clock")?.State);
        Assert.Equal(OdModuleState.Idle, shell.Library[0].State);
        Assert.Same(view, shell.CurrentView);

        await shell.SetWindowAwayAsync(away: false);
        Assert.Equal(OdModuleState.Active, host.Find("clock")?.State);
        Assert.Same(view, shell.CurrentView);

        // The stage keeps its view, so a module in full screen stays there.
        Assert.DoesNotContain(nameof(OdShellViewModel.CurrentView), changed, StringComparer.Ordinal);
        Assert.Equal("clock", shell.CurrentTarget);
    });

    [Fact]
    public void WindowBack_AfterAHibernation_ShowsTheNewView() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);
        await shell.NavigateAsync("clock");
        var view = shell.CurrentView;

        await shell.SetWindowAwayAsync(away: true);
        Assert.True(await host.HibernateAsync("clock"));
        Assert.Same(view, shell.CurrentView);

        await shell.SetWindowAwayAsync(away: false);
        Assert.NotNull(shell.CurrentView);
        Assert.NotSame(view, shell.CurrentView);
        Assert.Same(host.Find("clock")?.View, shell.CurrentView);
    });

    [Fact]
    public void ABrandName_IsShownAsWritten()
    {
        var item = new OdModuleItemViewModel(new OdModuleInfo("gmail", "Module_gmail", "Mail24", "Test", OdRamClass.Heavy, DisplayName: "Gmail"));

        Assert.Equal("Gmail", item.Title);
        Assert.Equal("Gmail", item.ToString());
    }

    private OdModuleHost NewHost() => new(
        [new OdFakeModule("clock"), new OdFakeModule("timer"), new OdFakeModule("calc")],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        new FakeTimeProvider(),
        new OdFakeNotifier(),
        OdTestHttp.Client);

    private OdShellViewModel NewShell(OdModuleHost host) => new(host, _settings, _lifetime);

    private sealed class OdFakeLifetime : IOdAppLifetime
    {
        public bool IsQuitting { get; private set; }

        public Task QuitAsync()
        {
            IsQuitting = true;
            return Task.CompletedTask;
        }
    }
}
