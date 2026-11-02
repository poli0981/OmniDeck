using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Runtime;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// A module in a window of its own (docs/ARCHITECTURE.md §2): its view under a title bar with its name,
/// full screen as in the main window. The view moves rather than being built again, so the browser or
/// video it hosts keeps running. Before closing, the window lets go of the view and hands the windows it
/// owns to the main window: LibVLCSharp's video overlay is owned by the window its video is in, and
/// would close for good with it.
/// </summary>
public partial class OdPopOutWindow
{
    private readonly Window _home;
    private readonly OdWindowCover _cover;
    private bool _released;

    internal OdPopOutWindow(OdModuleItemViewModel item, FrameworkElement view, Window home)
    {
        InitializeComponent();
        _home = home;
        _cover = new OdWindowCover(this);
        Title = item.Title;
        PopOutTitleBar.Title = item.Title;
        PopOutTitleBar.Icon = new SymbolIcon { Symbol = OdSymbolConverter.Parse(item.Info.Icon) };
        Stage.Content = view;
        AddHandler(OdFullscreen.RequestedEvent, new EventHandler<OdFullscreenRequestedEventArgs>(OnFullscreenRequested));
    }

    /// <summary>The user closed the window, which has let go of the view: the module goes back to the main window.</summary>
    internal event EventHandler? Returned;

    /// <summary>Where the window was, out of full screen, when it let go of the view.</summary>
    internal Rect LastBounds { get; private set; }

    /// <summary>Brings the window to the front, restored.</summary>
    internal void Present()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>Lets go of the view and closes: the module returns to the main window, or was closed.</summary>
    internal void Release()
    {
        if (_released)
        {
            return;
        }

        LetGo();
        Hide();
        CloseSoon();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!_released && !Dispatcher.HasShutdownStarted)
        {
            // The view goes first, and the window closes once it is gone (CloseSoon).
            e.Cancel = true;
            LetGo();
            Returned?.Invoke(this, EventArgs.Empty);
            CloseSoon();
        }

        base.OnClosing(e);
    }

    private void LetGo()
    {
        _released = true;
        SetFullscreen(on: false);
        LastBounds = RestoreBounds;
        Stage.Content = null;
    }

    // At a lower priority than the view's Unloaded, after which a video overlay no longer follows this
    // window's closing; the windows it still owns move to the main window.
    private void CloseSoon() => _ = Dispatcher.InvokeAsync(
        () =>
        {
            foreach (var owned in OwnedWindows.Cast<Window>().ToList())
            {
                owned.Owner = _home;
            }

            Close();
        },
        DispatcherPriority.ContextIdle);

    private void OnFullscreenRequested(object? sender, OdFullscreenRequestedEventArgs e)
    {
        e.Handled = true;
        SetFullscreen(e.On);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _cover.IsCovering)
        {
            e.Handled = true;
            SetFullscreen(on: false);
        }
    }

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

            PopOutTitleBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            _cover.Uncover();
            PopOutTitleBar.Visibility = Visibility.Visible;
        }

        OdFullscreen.SetIsActive(Stage, on);
    }
}
