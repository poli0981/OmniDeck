using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// Makes a window cover a monitor, on top with square corners, and puts it back where it was
/// (docs/spikes/06-libvlcsharp.md: the window covers the screen; the player never moves). Hiding the
/// window's own chrome is up to the window.
/// </summary>
internal sealed class OdWindowCover(FluentWindow window)
{
    private OdWindowPlacement? _before;

    public bool IsCovering => _before is not null;

    /// <summary>Covers <paramref name="monitor"/>, in device pixels; false, changing nothing, if it is empty or the window covers one already.</summary>
    public bool Cover(Int32Rect monitor)
    {
        if (_before is not null || monitor.IsEmpty)
        {
            return false;
        }

        _before = new OdWindowPlacement(
            window.WindowState,
            window.Left,
            window.Top,
            window.Width,
            window.Height,
            window.Topmost,
            window.ResizeMode,
            window.WindowCornerPreference);
        var dpi = VisualTreeHelper.GetDpi(window);
        window.WindowState = WindowState.Normal;
        window.WindowCornerPreference = WindowCornerPreference.DoNotRound;
        window.ResizeMode = ResizeMode.NoResize;
        window.Topmost = true;
        window.Left = monitor.X / dpi.DpiScaleX;
        window.Top = monitor.Y / dpi.DpiScaleY;
        window.Width = monitor.Width / dpi.DpiScaleX;
        window.Height = monitor.Height / dpi.DpiScaleY;
        return true;
    }

    /// <summary>Puts the window back as it was; false if it covered nothing.</summary>
    public bool Uncover()
    {
        if (_before is not { } before)
        {
            return false;
        }

        _before = null;
        window.WindowCornerPreference = before.Corners;
        window.Topmost = before.Topmost;
        window.ResizeMode = before.Resize;
        window.Left = before.Left;
        window.Top = before.Top;
        window.Width = before.Width;
        window.Height = before.Height;
        window.WindowState = before.State;
        return true;
    }
}
