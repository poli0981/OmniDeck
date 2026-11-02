using System.Windows;

namespace OmniDeck.Core.Modules;

/// <summary>A view asked to cover the screen (<see cref="On"/>), or to stop.</summary>
public sealed class OdFullscreenRequestedEventArgs(RoutedEvent routedEvent, object source, bool on) : RoutedEventArgs(routedEvent, source)
{
    public bool On { get; } = on;
}
