using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.App.Settings;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;

namespace OmniDeck.App.Shell;

/// <summary>
/// The shell's state: what the sidebar lists, what is showing (a module or the library page), the
/// view on the module stage, and which modules show in windows of their own. The navigation view is
/// chrome only (docs/WPFUI-CONVENTIONS.md §5): every navigation comes through <see cref="NavigateAsync"/>
/// and the module host runs the module. Used from the UI thread.
/// </summary>
public sealed partial class OdShellViewModel : ObservableObject, IDisposable
{
    /// <summary>The navigation target of the library page; module ids are the other targets.</summary>
    public const string LibraryTarget = "library";

    /// <summary>The navigation target of the settings page, whose Memory tab holds the running modules.</summary>
    public const string SettingsTarget = "settings";

    private static readonly string[] ShellPages = [LibraryTarget, SettingsTarget];

    private readonly OdModuleHost _host;
    private readonly IOdSettingsService _settings;
    private readonly IOdAppLifetime _lifetime;
    private readonly Dictionary<string, OdModuleItemViewModel> _items;
    private readonly List<OdModuleItemViewModel> _openOrder = [];
    private IReadOnlyList<string> _pinned = [];
    private string _sidebarShape = "";
    private bool _windowAway;

    [ObservableProperty]
    private string _currentTarget = LibraryTarget;

    [ObservableProperty]
    private FrameworkElement? _currentView;

    public OdShellViewModel(OdModuleHost host, IOdSettingsService settings, IOdAppLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(settings);
        _host = host;
        _settings = settings;
        _lifetime = lifetime;
        Library = [.. host.Modules.Select(hosted => new OdModuleItemViewModel(hosted.Info) { State = hosted.State })];
        _items = Library.ToDictionary(item => item.Id, StringComparer.Ordinal);
        ApplyPins(settings.Current.PinnedModules);
        host.StateChanged += OnModuleStateChanged;
        settings.Changed += OnSettingsChanged;
        OdLocalization.Instance.CultureChanged += OnCultureChanged;
    }

    /// <summary>Raised when a settings tab should come to the front, such as the palette's "Running modules".</summary>
    public event EventHandler<OdSettingsSectionEventArgs>? SettingsSectionRequested;

    /// <summary>A module moved into a window of its own, which shows <see cref="OdPopOutEventArgs.View"/>.</summary>
    public event EventHandler<OdPopOutEventArgs>? PopOutOpened;

    /// <summary>A module in a window of its own was asked for: that window comes to the front.</summary>
    public event EventHandler<OdPopOutEventArgs>? PopOutActivationRequested;

    /// <summary>A module's own window must let go of the view and close: the module returns to the main window, or closed.</summary>
    public event EventHandler<OdPopOutEventArgs>? PopOutCloseRequested;

    /// <summary>Every module, in registration order: the library page.</summary>
    public IReadOnlyList<OdModuleItemViewModel> Library { get; }

    public bool HasModules => Library.Count > 0;

    /// <summary>
    /// The sidebar: pinned modules in pin order, then open unpinned ones in the order they were opened.
    /// It is rebuilt whenever its order or a pin changes, so the window can draw the pinned group.
    /// </summary>
    public ObservableCollection<OdModuleItemViewModel> Sidebar { get; } = [];

    /// <summary>
    /// The sidebar as drawn: all of it, or the first <paramref name="max"/> modules when the preset caps it
    /// (<c>Od.Nav.MaxVisibleItems</c>, docs/UI-MODES.md §2). The module on screen always stays in view, in
    /// the last place; the others remain in the library and the palette.
    /// </summary>
    public IReadOnlyList<OdModuleItemViewModel> VisibleSidebar(int max)
    {
        if (max <= 0 || Sidebar.Count <= max)
        {
            return [.. Sidebar];
        }

        var visible = Sidebar.Take(max).ToList();
        if (_items.GetValueOrDefault(CurrentTarget) is { } current && !visible.Contains(current))
        {
            visible[^1] = current;
        }

        return visible;
    }

    /// <summary>
    /// Where a Ctrl shortcut leads (docs/SPEC.md: full keyboard navigation of the shell): Ctrl+1 to Ctrl+9
    /// open the modules shown in the sidebar, in order, and Ctrl+, opens Settings. Null for other keys.
    /// </summary>
    public string? ShortcutTarget(Key key, int maxVisible)
    {
        if (key == Key.OemComma)
        {
            return SettingsTarget;
        }

        var position = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1,
            _ => -1,
        };
        var visible = VisibleSidebar(maxVisible);
        return position >= 0 && position < visible.Count ? visible[position].Id : null;
    }

    /// <summary>
    /// Shows <paramref name="target"/>: a module id, or one of the shell's own pages. A module in a window of
    /// its own stays there, and its window comes to the front. Other targets are ignored.
    /// </summary>
    public async Task NavigateAsync(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ShellPages.Contains(target, StringComparer.Ordinal))
        {
            await _host.HideAsync();
            CurrentView = null;
            CurrentTarget = target;
        }
        else if (_items.TryGetValue(target, out var item))
        {
            if (item.IsPoppedOut)
            {
                PopOutActivationRequested?.Invoke(this, new OdPopOutEventArgs(item));
                return;
            }

            CurrentView = await _host.ShowAsync(target);
            CurrentTarget = target;
        }
    }

    /// <summary>
    /// The main window was minimized or hidden in the tray (<paramref name="away"/>), or is back. Away,
    /// its module goes down the ladder like any hidden one; back, it is active again. The stage keeps its
    /// view meanwhile, so a full screen and the page stay as they were; a module hibernated while away
    /// comes back with a new view.
    /// </summary>
    public async Task SetWindowAwayAsync(bool away)
    {
        if (away == _windowAway)
        {
            return;
        }

        _windowAway = away;
        if (await _host.SetMainWindowAwayAsync(away) is { } view)
        {
            CurrentView = view;
        }
    }

    /// <summary>The palette's entries in the current language: every module, then the shell commands.</summary>
    public IReadOnlyList<OdPaletteEntry> PaletteEntries()
    {
        var command = OdLocalization.Get("Shell_PaletteCommand");
        return
        [
            .. Library.Select(item => new OdPaletteEntry(item.Title, item.Category, item.Info.Icon, () => NavigateAsync(item.Id))),
            new(OdLocalization.Get("Shell_LibraryTitle"), command, "Apps24", () => NavigateAsync(LibraryTarget)),
            new(OdLocalization.Get("Settings_Title"), command, "Settings24", () => NavigateAsync(SettingsTarget)),
            new(OdLocalization.Get("Memory_Title"), command, "DataUsage24", () => ShowSettingsAsync(OdSettingsSection.Memory)),
            new(OdLocalization.Get("Shell_Quit"), command, "Power24", QuitAsync),
        ];
    }

    /// <summary>The user closed a module's own window: the module goes back to the main window, hidden.</summary>
    public Task DockAsync(OdModuleItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _host.DockAsync(item.Id);
    }

    public void Dispose()
    {
        _host.StateChanged -= OnModuleStateChanged;
        _settings.Changed -= OnSettingsChanged;
        OdLocalization.Instance.CultureChanged -= OnCultureChanged;
    }

    [RelayCommand]
    private Task OpenAsync(OdModuleItemViewModel item) => NavigateAsync(item.Id);

    [RelayCommand]
    private Task CloseAsync(OdModuleItemViewModel item) => _host.CloseAsync(item.Id);

    /// <summary>Moves a module into a window of its own; a main window that showed it shows the library instead.</summary>
    [RelayCommand]
    private async Task PopOutAsync(OdModuleItemViewModel item)
    {
        if (item.IsPoppedOut)
        {
            PopOutActivationRequested?.Invoke(this, new OdPopOutEventArgs(item));
            return;
        }

        if (string.Equals(item.Id, CurrentTarget, StringComparison.Ordinal))
        {
            // A view is in one window at a time: the main window lets go of it first.
            await NavigateAsync(LibraryTarget);
        }

        var view = await _host.PopOutAsync(item.Id);
        PopOutOpened?.Invoke(this, new OdPopOutEventArgs(item, view));
    }

    /// <summary>Brings a module back from its own window and shows it in the main window.</summary>
    [RelayCommand]
    private async Task PopInAsync(OdModuleItemViewModel item)
    {
        if (!item.IsPoppedOut)
        {
            return;
        }

        PopOutCloseRequested?.Invoke(this, new OdPopOutEventArgs(item));
        await _host.DockAsync(item.Id);
        await NavigateAsync(item.Id);
    }

    [RelayCommand]
    private void TogglePin(OdModuleItemViewModel item) => _settings.Update(s => s with
    {
        PinnedModules = item.IsPinned
            ? [.. s.PinnedModules.Where(id => !string.Equals(id, item.Id, StringComparison.Ordinal))]
            : [.. s.PinnedModules, item.Id],
    });

    private Task ShowSettingsAsync(OdSettingsSection section)
    {
        SettingsSectionRequested?.Invoke(this, new OdSettingsSectionEventArgs(section));
        return NavigateAsync(SettingsTarget);
    }

    private Task QuitAsync() => _lifetime.QuitAsync();

    private void OnModuleStateChanged(object? sender, OdModuleStateChangedEventArgs e)
    {
        if (!_items.TryGetValue(e.Module.Info.Id, out var item))
        {
            return;
        }

        var wasPoppedOut = item.IsPoppedOut;
        item.State = e.Module.State;
        item.IsPoppedOut = e.Module.IsPoppedOut;
        if (!item.IsOpen)
        {
            _openOrder.Remove(item);
            if (wasPoppedOut)
            {
                // Closed in its own window: the window goes too.
                PopOutCloseRequested?.Invoke(this, new OdPopOutEventArgs(item));
            }
            else if (string.Equals(item.Id, CurrentTarget, StringComparison.Ordinal))
            {
                // Closed while showing: the library takes its place.
                CurrentView = null;
                CurrentTarget = LibraryTarget;
            }
        }
        else if (!_openOrder.Contains(item))
        {
            _openOrder.Add(item);
        }

        UpdateSidebar();
    }

    private void OnSettingsChanged(object? sender, OdSettingsChangedEventArgs e) => ApplyPins(e.Settings.PinnedModules);

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var item in Library)
        {
            item.RefreshTexts();
        }
    }

    private void ApplyPins(IReadOnlyList<string> pinned)
    {
        _pinned = pinned;
        foreach (var item in Library)
        {
            item.IsPinned = pinned.Contains(item.Id, StringComparer.Ordinal);
        }

        UpdateSidebar();
    }

    private void UpdateSidebar()
    {
        // Pins may name modules this build does not have (a web app taken out of the catalog): skip them.
        OdModuleItemViewModel[] wanted =
        [
            .. _pinned.Select(id => _items.GetValueOrDefault(id)).OfType<OdModuleItemViewModel>(),
            .. _openOrder.Where(item => !item.IsPinned),
        ];
        var shape = string.Join(' ', wanted.Select(item => item.IsPinned ? "+" + item.Id : item.Id));
        if (string.Equals(shape, _sidebarShape, StringComparison.Ordinal))
        {
            return;
        }

        _sidebarShape = shape;
        Sidebar.Clear();
        foreach (var item in wanted)
        {
            Sidebar.Add(item);
        }
    }
}
