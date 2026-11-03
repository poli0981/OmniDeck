using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using H.NotifyIcon.Core;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Legal;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Runtime;
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

    // How far below and right of this window's corner a module's own window first opens.
    private const double PopOutOffset = 48;

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
    private readonly OdModuleHost _host;
    private readonly OdWindowCover _cover;
    private readonly Dictionary<string, OdPopOutWindow> _popOuts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _popOutBounds = new(StringComparer.Ordinal);
    private OdPanePlacement? _paneBeforeFullscreen;
    private readonly Dictionary<string, OdNavItem> _moduleItems = new(StringComparer.Ordinal);
    private readonly NavigationViewItemSeparator _pinnedSeparator = new();
    private readonly OdTrayIcon _trayIcon = new();
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
        IOdQuickTimer quickTimer,
        OdBugReportFlow bugReports,
        OdBugReportRequests bugReportRequests,
        OdModuleHost host)
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
        ListenForRequests(shell, settingsPage, legalGate, bugReports, bugReportRequests);
        _stage = new OdModuleStage { DataContext = shell };
        _host = host;
        _cover = new OdWindowCover(this);
        ListenForFullscreen(shell);
        ListenForPopOuts(shell);

        // A web app's browser is a native window over the stage that no dialog can cover: hide it meanwhile.
        dialogs.OpenChanged += (_, _) => _stage.Visibility = dialogs.IsOpen ? Visibility.Hidden : Visibility.Visible;

        dialogs.SetHost(RootDialogHost);
        snackbars.SetSnackbarPresenter(RootSnackbar);

        // Created now, so the icon exists even if the window never shows; no Efficiency Mode, which
        // would throttle the whole process while timers run in the background. The image comes first,
        // so the icon never appears blank.
        UpdateTrayIcon();
        TrayIcon.ForceCreate(enablesEfficiencyMode: false);
        notifier.Attach((title, message) => _ = Dispatcher.InvokeAsync(() => TrayIcon.ShowNotification(title, message, NotificationIcon.Info)));
        notifier.Clicked += OnNotificationClicked;
        Closed += (_, _) =>
        {
            notifier.Clicked -= OnNotificationClicked;
            TrayIcon.Dispose();
        };

        SetUpNavigation(theme);
        ListenForAway();

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

    /// <summary>What the palette, the settings page and a crash ask the window to show.</summary>
    private void ListenForRequests(OdShellViewModel shell, OdSettingsViewModel settingsPage, OdLegalGate legalGate, OdBugReportFlow bugReports, OdBugReportRequests bugReportRequests)
    {
        shell.SettingsSectionRequested += (_, e) => settingsPage.ShowSection(e.Section);
        shell.CommandRequested += (_, e) => Run(e.Command, settingsPage, bugReports);
        settingsPage.About.LegalRequested += (_, _) =>
            new OdLegalWindow(new OdLegalViewModel(legalGate, OdApp.LegalFolder, readOnly: true)) { Owner = this }.Show();
        settingsPage.About.BugReportRequested += (sender, args) => _ = bugReports.ShowAsync(error: null);
        bugReportRequests.Requested += (sender, args) => _ = bugReports.ShowAsync(args.Error);
    }

    /// <summary>Carries out a command the palette ran.</summary>
    private static void Run(OdShellCommand command, OdSettingsViewModel settingsPage, OdBugReportFlow bugReports)
    {
        switch (command)
        {
            case OdShellCommand.CheckForUpdates when settingsPage.About.CheckCommand.CanExecute(null):
                _ = settingsPage.About.CheckCommand.ExecuteAsync(null);
                break;
            case OdShellCommand.ReportBug:
                _ = bugReports.ShowAsync(error: null);
                break;
            case OdShellCommand.OpenDataFolder:
                settingsPage.OpenDataFolderCommand.Execute(null);
                break;
        }
    }

    /// <summary>A sidebar icon at the preset's size: larger in Comfort (docs/UI-MODES.md §2).</summary>
    private static SymbolIcon NavIcon(SymbolRegular symbol)
    {
        var icon = new SymbolIcon { Symbol = symbol };
        icon.SetResourceReference(FontSizeProperty, OdTokenKeys.NavIconSize);
        return icon;
    }

    /// <summary>
    /// A sidebar item screen readers can use (<see cref="OdNavItem"/>), at the preset's text size and
    /// minimum hit target (docs/UI-MODES.md §2): WPF-UI draws its labels at 14 px and its items 40 px
    /// tall whatever the preset. Being a subclass, it takes WPF-UI's style by name.
    /// </summary>
    private OdNavItem NavItem(SymbolRegular icon, string target, Type pageType)
    {
        var item = new OdNavItem
        {
            Style = (Style)FindResource(typeof(NavigationViewItem)),
            Icon = NavIcon(icon),
            TargetPageTag = target,
            TargetPageType = pageType,
            ContentTemplate = (DataTemplate)FindResource("NavLabel"),
        };
        item.SetResourceReference(MinHeightProperty, OdTokenKeys.HitTargetMin);
        return item;
    }

    private OdNavItem ShellItem(string target, Binding title, SymbolRegular icon, Type pageType)
    {
        var item = NavItem(icon, target, pageType);
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

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        ApplyNavTokens();
        UpdateTrayIcon();
    }

    /// <summary>Redraws the tray icon when the accent changed; the taskbar icon disposes the one it replaces.</summary>
    private void UpdateTrayIcon()
    {
        if (_trayIcon.Update(TryFindResource("AccentFillColorDefaultBrush") as Brush, TryFindResource("TextOnAccentFillColorPrimaryBrush") as Brush) is { } icon)
        {
            TrayIcon.Icon = icon;
        }
    }

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

    private OdNavItem ModuleItem(OdModuleItemViewModel module)
    {
        var pin = new MenuItem { Command = _shell.TogglePinCommand, CommandParameter = module };
        var popOut = new MenuItem { CommandParameter = module };
        var close = new MenuItem { Command = _shell.CloseCommand, CommandParameter = module };
        close.SetBinding(HeaderedItemsControl.HeaderProperty, Localized("Shell_Close"));
        var menu = new ContextMenu { Items = { pin, popOut, close } };
        menu.Opened += (_, _) =>
        {
            pin.Header = OdLocalization.Get(module.IsPinned ? "Shell_Unpin" : "Shell_Pin");
            popOut.Header = module.IsPoppedOut ? OdLocalization.Get("Shell_PopIn") : OdLocalization.Get("Shell_PopOut");
            popOut.Command = module.IsPoppedOut ? _shell.PopInCommand : _shell.PopOutCommand;
        };

        var item = NavItem(OdSymbolConverter.Parse(module.Info.Icon), module.Id, typeof(OdModuleStage));
        item.ContextMenu = menu;
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

            // A module in a window of its own stays there: the selection goes back to what this window shows.
            if (!string.Equals(target, _shell.CurrentTarget, StringComparison.Ordinal))
            {
                RootNavigation.Navigate(_shell.CurrentTarget);
            }
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
        if (e.Key == Key.Escape && _cover.IsCovering)
        {
            e.Handled = true;
            SetFullscreen(on: false);
            return;
        }

        // Exactly Ctrl: AltGr arrives as Ctrl+Alt, and must type its character rather than run a shortcut.
        if (Keyboard.Modifiers != ModifierKeys.Control || _dialogs.IsOpen)
        {
            return;
        }

        if (e.Key == Key.K)
        {
            e.Handled = true;
            await OpenPaletteAsync(this);
        }
        else if (_shell.ShortcutTarget(e.Key, NavMaxVisibleItems) is { } target)
        {
            e.Handled = true;
            await _shell.NavigateAsync(target);
            FocusPageSoon();
        }
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) => await OpenPaletteAsync(this);

    /// <summary>
    /// The command palette over <paramref name="owner"/>. What it runs happens in the main window, which
    /// comes forward first when the palette was opened from a pop-out; a popped-out module brings its own
    /// window back to the front.
    /// </summary>
    private async Task OpenPaletteAsync(Window owner)
    {
        var palette = new OdCommandPaletteViewModel(_shell.PaletteEntries());
        new OdCommandPaletteWindow(palette, owner).ShowDialog();
        if (palette.Chosen is { } chosen)
        {
            if (owner != this)
            {
                Present();
            }

            await chosen.RunAsync();
            if (!_dialogs.IsOpen)
            {
                FocusPageSoon();
            }
        }
    }

    /// <summary>
    /// Moves the keyboard into the page on screen once it is shown, as after a palette choice or a Ctrl+1–9
    /// shortcut: the keyboard otherwise stays where the palette left it, outside the page.
    /// </summary>
    private void FocusPageSoon() => _ = Dispatcher.InvokeAsync(
        () =>
        {
            FrameworkElement page = _shell.CurrentTarget switch
            {
                OdShellViewModel.LibraryTarget => _library,
                OdShellViewModel.SettingsTarget => _settingsPage,
                _ => _stage,
            };
            if (IsActive && !page.IsKeyboardFocusWithin)
            {
                page.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        },
        DispatcherPriority.Input);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_lifetime.IsQuitting || !_settings.Current.CloseToTray)
        {
            return;
        }

        e.Cancel = true;
        SetFullscreen(on: false);
        Hide();

        // Say once, ever, where the window went.
        if (_meta.Get(OdAppMetaKeys.TrayHintShown) is null)
        {
            _meta.Set(OdAppMetaKeys.TrayHintShown, "1");
            _notifier.Notify(OdLocalization.Get("Tray_StillRunningTitle"), OdLocalization.Get("Tray_StillRunningMessage"));
        }
    }

    private void OnTrayOpen(object sender, RoutedEventArgs e) => Present();

    private void OnTrayNotificationClicked(object sender, RoutedEventArgs e) => _notifier.OnClicked();

    /// <summary>Minimized or in the tray, the window shows nothing: its module goes down the ladder meanwhile.</summary>
    private void ListenForAway()
    {
        IsVisibleChanged += (_, _) => OnAwayChanged(this, EventArgs.Empty);
        StateChanged += OnAwayChanged;
    }

    private async void OnAwayChanged(object? sender, EventArgs e) =>
        await _shell.SetWindowAwayAsync(!IsVisible || WindowState == WindowState.Minimized);

    /// <summary>
    /// A click on a notification brings the window up, on the module that raised it. It navigates first,
    /// so a module the window showed before is not woken up only to be hidden again.
    /// </summary>
    private async void OnNotificationClicked(object? sender, OdNotificationClickedEventArgs e)
    {
        try
        {
            if (e.Target is { } target)
            {
                await _shell.NavigateAsync(target);
            }
        }
        finally
        {
            Present();
        }
    }

    private async void OnTrayQuit(object sender, RoutedEventArgs e) => await _lifetime.QuitAsync();

    private void OnTrayQuickTimer(object sender, RoutedEventArgs e) => _quickTimer.StartQuick(TimeSpan.FromMinutes(5));

    private IOdMediaControls? LoadedMedia() =>
        _host.Modules.Select(hosted => hosted.Module).OfType<IOdMediaControls>().FirstOrDefault(media => media.NowPlaying is not null);

    /// <summary>The media items show only while a media module has something loaded.</summary>
    private void OnTrayMenuOpened(object sender, RoutedEventArgs e)
    {
        var media = LoadedMedia();
        var visibility = media is null ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in new FrameworkElement[] { TrayMediaSeparator, TrayNowPlaying, TrayPlayPause, TrayNext, TrayPrevious })
        {
            item.Visibility = visibility;
        }

        if (media is not null)
        {
            TrayNowPlaying.Header = media.NowPlaying;
            TrayPlayPause.Header = media.IsPlaying ? OdLocalization.Get("Tray_Pause") : OdLocalization.Get("Tray_Play");
        }
    }

    private void OnTrayPlayPause(object sender, RoutedEventArgs e) => LoadedMedia()?.PlayPause();

    private void OnTrayNext(object sender, RoutedEventArgs e) => LoadedMedia()?.Next();

    private void OnTrayPrevious(object sender, RoutedEventArgs e) => LoadedMedia()?.Previous();

    private void ListenForFullscreen(OdShellViewModel shell)
    {
        AddHandler(OdFullscreen.RequestedEvent, new EventHandler<OdFullscreenRequestedEventArgs>(OnFullscreenRequested));
        shell.PropertyChanged += (_, e) =>
        {
            // Full screen belongs to the module on screen: showing another one ends it.
            if (string.Equals(e.PropertyName, nameof(OdShellViewModel.CurrentView), StringComparison.Ordinal))
            {
                SetFullscreen(on: false);
            }
        };
    }

    private void ListenForPopOuts(OdShellViewModel shell)
    {
        shell.PopOutOpened += (_, e) => OpenPopOut(e.Item, e.View!);
        shell.PopOutActivationRequested += (_, e) => _popOuts.GetValueOrDefault(e.Item.Id)?.Present();
        shell.PopOutCloseRequested += (_, e) =>
        {
            if (_popOuts.Remove(e.Item.Id, out var window))
            {
                window.Release();
                _popOutBounds[e.Item.Id] = window.LastBounds;
            }
        };
    }

    /// <summary>Opens a module's own window where it was last in this session, or beside this window.</summary>
    private void OpenPopOut(OdModuleItemViewModel item, FrameworkElement view)
    {
        var window = new OdPopOutWindow(item, view, this);
        window.PaletteRequested += async (_, _) => await OpenPaletteAsync(window);
        if (_popOutBounds.TryGetValue(item.Id, out var bounds))
        {
            window.Left = bounds.Left;
            window.Top = bounds.Top;
            window.Width = bounds.Width;
            window.Height = bounds.Height;
        }
        else if (IsVisible && WindowState == WindowState.Normal)
        {
            window.Left = Left + PopOutOffset;
            window.Top = Top + PopOutOffset;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.Returned += async (_, _) =>
        {
            _popOuts.Remove(item.Id);
            _popOutBounds[item.Id] = window.LastBounds;
            await _shell.DockAsync(item);
        };
        _popOuts[item.Id] = window;
        window.Show();
    }

    private void OnFullscreenRequested(object? sender, OdFullscreenRequestedEventArgs e)
    {
        e.Handled = true;
        SetFullscreen(e.On);
    }

    /// <summary>Covers the monitor the window is on, chrome hidden, or puts the window back where it was.</summary>
    private void SetFullscreen(bool on)
    {
        if (on == _cover.IsCovering)
        {
            return;
        }

        if (on)
        {
            if (!_cover.Cover(OdMonitors.BoundsOf(new WindowInteropHelper(this).Handle)))
            {
                return;
            }

            FoldChrome();
        }
        else
        {
            _cover.Uncover();
            UnfoldChrome();
        }

        OdFullscreen.SetIsActive(_stage, on);
    }

    private void FoldChrome()
    {
        _paneBeforeFullscreen = new OdPanePlacement(RootNavigation.OpenPaneLength, RootNavigation.CompactPaneLength, RootNavigation.IsPaneToggleVisible, RootNavigation.FrameMargin);
        AppTitleBar.Visibility = Visibility.Collapsed;
        RootNavigation.TitleBar = null;

        // The pane folds to nothing: changing its display mode would re-template the navigation view and
        // re-create the stage, and with it a player's video window.
        RootNavigation.IsPaneToggleVisible = false;
        RootNavigation.OpenPaneLength = 0;
        RootNavigation.CompactPaneLength = 0;

        // The folded pane's template still keeps a few pixels (its margins, the frame's border): the frame
        // overlaps them. Clearing the title bar has just reset the frame's margin, so it is read only now.
        UpdateLayout();
        var offset = _stage.TranslatePoint(default, this);
        var frame = RootNavigation.FrameMargin;
        RootNavigation.FrameMargin = new Thickness(frame.Left - offset.X, frame.Top - offset.Y, frame.Right, frame.Bottom);
    }

    private void UnfoldChrome()
    {
        AppTitleBar.Visibility = Visibility.Visible;
        RootNavigation.TitleBar = AppTitleBar;
        if (_paneBeforeFullscreen is { } pane)
        {
            _paneBeforeFullscreen = null;
            RootNavigation.OpenPaneLength = pane.OpenLength;
            RootNavigation.CompactPaneLength = pane.CompactLength;
            RootNavigation.IsPaneToggleVisible = pane.ToggleVisible;
            RootNavigation.FrameMargin = pane.FrameMargin;
        }
    }
}
