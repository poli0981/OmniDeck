using System.ComponentModel;
using Microsoft.Extensions.Time.Testing;
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
    public void PaletteEntries_OpenModules_ShowTheLibrary_AndQuit() => OdSta.Run(async () =>
    {
        using var host = NewHost();
        using var shell = NewShell(host);

        var entries = shell.PaletteEntries();

        Assert.Equal(new[] { "Module_clock", "Module_timer", "Module_calc", "Module library", "Running modules", "Quit OmniDeck" }, entries.Select(e => e.Title), StringComparer.Ordinal);
        Assert.Equal("Command", entries[^1].Detail);
        await entries[1].RunAsync();
        Assert.Equal("timer", shell.CurrentTarget);
        await entries[4].RunAsync();
        Assert.Equal(OdShellViewModel.MemoryTarget, shell.CurrentTarget);
        Assert.Null(host.Current);
        await entries[3].RunAsync();
        Assert.Equal(OdShellViewModel.LibraryTarget, shell.CurrentTarget);
        Assert.False(_lifetime.IsQuitting);
        await entries[5].RunAsync();
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

    private OdModuleHost NewHost() => new(
        [new OdFakeModule("clock"), new OdFakeModule("timer"), new OdFakeModule("calc")],
        new OdDatabase(_temp.Combine("omnideck.db"), pooling: false),
        Logger.None,
        new FakeTimeProvider(),
        new OdFakeNotifier());

    private OdShellViewModel NewShell(OdModuleHost host) => new(host, _settings, _lifetime);

    private sealed class OdFakeLifetime : IOdAppLifetime
    {
        public bool IsQuitting { get; private set; }

        public void Quit() => IsQuitting = true;
    }
}
