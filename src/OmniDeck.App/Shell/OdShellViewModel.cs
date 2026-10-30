using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;

namespace OmniDeck.App.Shell;

/// <summary>
/// The shell's state: what the sidebar lists, what is showing (a module or the library page) and the
/// view on the module stage. The navigation view is chrome only (docs/WPFUI-CONVENTIONS.md §5): every
/// navigation comes through <see cref="NavigateAsync"/> and the module host runs the module. Used from
/// the UI thread.
/// </summary>
public sealed partial class OdShellViewModel : ObservableObject, IDisposable
{
    /// <summary>The navigation target of the library page; module ids are the other targets.</summary>
    public const string LibraryTarget = "library";

    private readonly OdModuleHost _host;
    private readonly IOdSettingsService _settings;
    private readonly IOdAppLifetime _lifetime;
    private readonly Dictionary<string, OdModuleItemViewModel> _items;
    private readonly List<OdModuleItemViewModel> _openOrder = [];
    private IReadOnlyList<string> _pinned = [];
    private string _sidebarShape = "";

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

    /// <summary>Every module, in registration order: the library page.</summary>
    public IReadOnlyList<OdModuleItemViewModel> Library { get; }

    public bool HasModules => Library.Count > 0;

    /// <summary>
    /// The sidebar: pinned modules in pin order, then open unpinned ones in the order they were opened.
    /// It is rebuilt whenever its order or a pin changes, so the window can draw the pinned group.
    /// </summary>
    public ObservableCollection<OdModuleItemViewModel> Sidebar { get; } = [];

    /// <summary>Shows <paramref name="target"/>, a module id or <see cref="LibraryTarget"/>; other targets are ignored.</summary>
    public async Task NavigateAsync(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.Equals(target, LibraryTarget, StringComparison.Ordinal))
        {
            await _host.HideAsync();
            CurrentView = null;
            CurrentTarget = LibraryTarget;
        }
        else if (_items.ContainsKey(target))
        {
            CurrentView = await _host.ShowAsync(target);
            CurrentTarget = target;
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
            new(OdLocalization.Get("Shell_Quit"), command, "Power24", QuitAsync),
        ];
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

    [RelayCommand]
    private void TogglePin(OdModuleItemViewModel item) => _settings.Update(s => s with
    {
        PinnedModules = item.IsPinned
            ? [.. s.PinnedModules.Where(id => !string.Equals(id, item.Id, StringComparison.Ordinal))]
            : [.. s.PinnedModules, item.Id],
    });

    private Task QuitAsync()
    {
        _lifetime.Quit();
        return Task.CompletedTask;
    }

    private void OnModuleStateChanged(object? sender, OdModuleStateChangedEventArgs e)
    {
        if (!_items.TryGetValue(e.Module.Info.Id, out var item))
        {
            return;
        }

        item.State = e.Module.State;
        if (!item.IsOpen)
        {
            _openOrder.Remove(item);
            if (string.Equals(item.Id, CurrentTarget, StringComparison.Ordinal))
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
