using System.Windows;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>Where a window was, and how it looked, before it covered the screen.</summary>
internal sealed record OdWindowPlacement(
    WindowState State,
    double Left,
    double Top,
    double Width,
    double Height,
    bool Topmost,
    ResizeMode Resize,
    WindowCornerPreference Corners);
