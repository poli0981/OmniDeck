using System.Windows;

namespace OmniDeck.Core.Modules;

/// <summary>
/// How a module's view asks the window it is in to cover the screen, and learns that it does
/// (docs/SPEC.md §3.6: the module's own window covers the monitor). The request bubbles up to the
/// window as a routed event; the window answers by setting <see cref="IsActiveProperty"/> on its
/// module stage, which views inherit. A view raises the request from itself: content it hosts in a
/// window of its own, such as a video overlay, is outside the window's tree.
/// </summary>
public static class OdFullscreen
{
    public static readonly RoutedEvent RequestedEvent = EventManager.RegisterRoutedEvent(
        "Requested", RoutingStrategy.Bubble, typeof(EventHandler<OdFullscreenRequestedEventArgs>), typeof(OdFullscreen));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive", typeof(bool), typeof(OdFullscreen), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Asks the window holding <paramref name="view"/> to cover the screen, or to stop.</summary>
    public static void Request(UIElement view, bool on)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.RaiseEvent(new OdFullscreenRequestedEventArgs(RequestedEvent, view, on));
    }

    public static bool GetIsActive(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsActiveProperty);
    }

    public static void SetIsActive(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsActiveProperty, value);
    }
}
