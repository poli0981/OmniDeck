using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using H.NotifyIcon.Core;
using OmniDeck.App.Legal;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Core.Theming;
using OmniDeck.Modules.Timer;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace OmniDeck.App;

/// <summary>
/// The shell window: the title bar with the palette button, the navigation view as sidebar chrome,
/// the module stage, in-window dialogs and snackbars, and the tray icon. Closing hides it into the
/// tray unless the user turned that off or is quitting.
/// </summary>
public partial class OdMainWindow : INavigationViewPageProvider
{
    private const int WmSettingChange = 0x001A;

    private readonly OdShellViewModel _shell;
    private readonly OdDialogs _dialogs;
    private readonly OdTrayNotifier _notifier;
    private readonly IOdSettingsService _settings;
    private readonly IOdAppLifetime _lifetime;
    private readonly IOdAppMeta _meta;
    private readonly IOdQuickTimer _quickTimer;
    private readonly OdLibraryPage _library;
    private readonly OdSettingsPage _settingsPage;
    private readonly OdModuleStage _stage;
    private readonly Dictionary<string, NavigationViewItem> _moduleItems = new(StringComparer.Ordinal);
    private readonly NavigationViewItemSeparator _pinnedSeparator = new();
    private bool? _navLabels;
    private int _navMax = -1;

    public OdMainWindow(
        OdThemeEngine theme,
        OdShellViewModel shell,
        OdSettingsViewModel settingsPage,
        OdLegalGate legalGate,
        OdDialogs dialogs,
        ISnackbarService snackbars,
        OdTrayNotifier notifier,
        IOdSettingsService settings,
        IOdAppLifetime lifetime,
        IOdAppMeta meta,
        IOdQuickTimer quickTimer)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settingsPage);
        ArgumentNullException.ThrowIfNull(snackbars);
        InitializeComponent();
        _shell = shell;
        _dialogs = dialogs;
        _notifier = notifier;
        _settings = settings;
        _lifetime = lifetime;
        _meta = meta;
        _quickTimer = quickTimer;
        DataContext = shell;
        _library = new OdLibraryPage { DataContext = shell };
        _settingsPage = new OdSettingsPage { DataContext = settingsPage };
        shell.SettingsSectionRequested += (_, e) => settingsPage.ShowSection(e.Section);
        settingsPage.About.LegalRequested += (_, _) =>
            new OdLegalWindow(new OdLegalViewModel(legalGate, OdApp.LegalFolder, readOnly: true)) { Owner = this }.Show();
        _stage = new OdModuleStage { DataContext = shell };

        dialogs.SetHost(RootDialogHost);
        snackbars.SetSnackbarPresenter(RootSnackbar);

        // Created now, so the icon exists even if the window never shows; no Efficiency Mode, which
        // would throttle the whole process while timers run in the background.
        TrayIcon.ForceCreate(enablesEfficiencyMode: false);
        notifier.Attach((title, message) => _ = Dispatcher.InvokeAsync(() => TrayIcon.ShowNotification(title, message, NotificationIcon.Info)));
        Closed += (_, _) => TrayIcon.Dispose();

        SetUpNavigation(theme);

        // WPF ignores the Windows text size; re-read it when it may have changed (docs/UI-MODES.md §4).
        Activated += (_, _) => theme.RefreshTextScale();
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(
            (IntPtr _, int msg, IntPtr _, IntPtr _, ref bool _) =>
            {
                if (msg == WmSettingChange)
                {
                    theme.RefreshTextScale();
                }

                return IntPtr.Zero;
            });
    }

    /// <summary>Shows the window from the tray or the taskbar, restored and in front.</summary>
    public void Present()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    object? INavigationViewPageProvider.GetPage(Type pageType)
    {
        if (pageType == typeof(OdLibraryPage))
        {
            return _library;
        }

        if (pageType == typeof(OdSettingsPage))
        {
            return _settingsPage;
        }

        return pageType == typeof(OdModuleStage) ? _stage : null;
    }

    private void SetUpNavigation(OdThemeEngine theme)
    {
        RootNavigation.SetPageProviderService(this);
        RootNavigation.FooterMenuItems.Add(ShellItem(OdShellViewModel.SettingsTarget, Localized("Settings_Nav"), SymbolRegular.Settings24, typeof(OdSettingsPage)));
        RootNavigation.FooterMenuItems.Add(ShellItem(OdShellViewModel.LibraryTarget, Localized("Shell_Library"), SymbolRegular.Apps24, typeof(OdLibraryPage)));
        SyncSidebar();
        _shell.Sidebar.CollectionChanged += (_, _) => SyncSidebar();
        _shell.PropertyChanged += OnShellPropertyChanged;
        RootNavigation.SelectionChanged += OnNavigationSelectionChanged;
        RootNavigation.Navigating += (_, _) => RootNavigation.TransitionDuration = (int)OdMotion.DurationFor(this).TotalMilliseconds;
        RootNavigation.PaneOpened += (_, _) => UpdateToolTips();
        RootNavigation.PaneClosed += (_, _) => UpdateToolTips();
        theme.Applied += OnThemeApplied;
        Closed += (_, _) => theme.Applied -= OnThemeApplied;
        Loaded += (_, _) =>
        {
            ApplyNavTokens();
            UpdateToolTips();
            RootNavigation.Navigate(_shell.CurrentTarget);
        };
    }

    private static Binding Localized(string key) => new($"[{key}]") { Source = OdLocalization.Instance, Mode = BindingMode.OneWay };

    private static NavigationViewItem ShellItem(string target, Binding title, SymbolRegular icon, Type pageType)
    {
        var item = new NavigationViewItem
        {
            Icon = new SymbolIcon { Symbol = icon },
            TargetPageTag = target,
            TargetPageType = pageType,
        };
        item.SetBinding(ContentControl.ContentProperty, title);
        item.SetBinding(AutomationProperties.NameProperty, title);
        item.SetBinding(ToolTipProperty, title);
        return item;
    }

    /// <summary>
    /// Mirrors the sidebar, with a separator between pinned and merely open modules. Each module keeps
    /// one item for the window's lifetime: the navigation view never forgets an item's tag, so a
    /// re-added module must come back as the same item.
    /// </summary>
    private void SyncSidebar()
    {
        RootNavigation.MenuItems.Clear();
        var previousPinned = false;
        foreach (var module in _shell.VisibleSidebar(NavMaxVisibleItems))
        {
            if (previousPinned && !module.IsPinned)
            {
                RootNavigation.MenuItems.Add(_pinnedSeparator);
            }

            if (!_moduleItems.TryGetValue(module.Id, out var item))
            {
                item = ModuleItem(module);
                _moduleItems[module.Id] = item;
            }

            RootNavigation.MenuItems.Add(item);
            previousPinned = module.IsPinned;
        }

        UpdateToolTips();
    }

    private int NavMaxVisibleItems => TryFindResource(OdTokenKeys.NavMaxVisibleItems) is int max ? max : 0;

    private void OnThemeApplied(object? sender, EventArgs e) => ApplyNavTokens();

    /// <summary>
    /// Follows the preset's sidebar tokens (docs/UI-MODES.md §2): labels or icons only, and how many modules
    /// it shows. Only a change of preset opens or closes the pane; otherwise the user's own toggle stands.
    /// </summary>
    private void ApplyNavTokens()
    {
        var labels = TryFindResource(OdTokenKeys.NavShowLabels) is not false;
        if (_navLabels != labels)
        {
            _navLabels = labels;
            RootNavigation.IsPaneOpen = labels;
        }

        var max = NavMaxVisibleItems;
        if (_navMax != max)
        {
            _navMax = max;
            SyncSidebar();
        }
    }

    /// <summary>With the pane closed the sidebar shows icons only, so each item's label moves into its tooltip.</summary>
    private void UpdateToolTips()
    {
        var show = !RootNavigation.IsPaneOpen;
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>().Concat(RootNavigation.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            ToolTipService.SetIsEnabled(item, show);
        }
    }

    private NavigationViewItem ModuleItem(OdModuleItemViewModel module)
    {
        var pin = new MenuItem { Command = _shell.TogglePinCommand, CommandParameter = module };
        var close = new MenuItem { Command = _shell.CloseCommand, CommandParameter = module };
        close.SetBinding(HeaderedItemsControl.HeaderProperty, Localized("Shell_Close"));
        var menu = new ContextMenu { Items = { pin, close } };
        menu.Opened += (_, _) => pin.Header = OdLocalization.Get(module.IsPinned ? "Shell_Unpin" : "Shell_Pin");

        var item = new NavigationViewItem
        {
            Icon = new SymbolIcon { Symbol = OdSymbolConverter.Parse(module.Info.Icon) },
            TargetPageTag = module.Id,
            TargetPageType = typeof(OdModuleStage),
            ContextMenu = menu,
        };
        item.SetBinding(ContentControl.ContentProperty, new Binding(nameof(OdModuleItemViewModel.Title)) { Source = module });
        item.SetBinding(AutomationProperties.NameProperty, new Binding(nameof(OdModuleItemViewModel.Title)) { Source = module });
        item.SetBinding(ToolTipProperty, new Binding(nameof(OdModuleItemViewModel.Title)) { Source = module });
        return item;
    }

    private async void OnNavigationSelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is NavigationViewItem { TargetPageTag: { Length: > 0 } target })
        {
            await _shell.NavigateAsync(target);
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(OdShellViewModel.CurrentTarget), StringComparison.Ordinal))
        {
            return;
        }

        // A capped sidebar must make room for the module now on screen before it can be selected.
        var max = NavMaxVisibleItems;
        if (max > 0 && _shell.Sidebar.Count > max)
        {
            SyncSidebar();
        }

        var selected = (RootNavigation.SelectedItem as NavigationViewItem)?.TargetPageTag;
        if (!string.Equals(selected, _shell.CurrentTarget, StringComparison.Ordinal))
        {
            RootNavigation.Navigate(_shell.CurrentTarget);
        }
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Exactly Ctrl: AltGr arrives as Ctrl+Alt, and must type its character rather than run a shortcut.
        if (Keyboard.Modifiers != ModifierKeys.Control || _dialogs.IsOpen)
        {
            return;
        }

        if (e.Key == Key.K)
        {
            e.Handled = true;
            await OpenPaletteAsync();
        }
        else if (_shell.ShortcutTarget(e.Key, NavMaxVisibleItems) is { } target)
        {
            e.Handled = true;
            await _shell.NavigateAsync(target);
        }
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) => await OpenPaletteAsync();

    private async Task OpenPaletteAsync()
    {
        var palette = new OdCommandPaletteViewModel(_shell.PaletteEntries());
        new OdCommandPaletteWindow(palette, this).ShowDialog();
        if (palette.Chosen is { } chosen)
        {
            await chosen.RunAsync();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_lifetime.IsQuitting || !_settings.Current.CloseToTray)
        {
            return;
        }

        e.Cancel = true;
        Hide();

        // Say once, ever, where the window went.
        if (_meta.Get(OdAppMetaKeys.TrayHintShown) is null)
        {
            _meta.Set(OdAppMetaKeys.TrayHintShown, "1");
            _notifier.Notify(OdLocalization.Get("Tray_StillRunningTitle"), OdLocalization.Get("Tray_StillRunningMessage"));
        }
    }

    private void OnTrayOpen(object sender, RoutedEventArgs e) => Present();

    private void OnTrayQuit(object sender, RoutedEventArgs e) => _lifetime.Quit();

    private void OnTrayQuickTimer(object sender, RoutedEventArgs e) => _quickTimer.StartQuick(TimeSpan.FromMinutes(5));
}
