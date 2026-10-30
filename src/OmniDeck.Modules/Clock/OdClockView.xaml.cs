using System.Windows;

namespace OmniDeck.Modules.Clock;

/// <summary>The world clock; it ticks only while it is on screen.</summary>
public partial class OdClockView
{
    public OdClockView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => (DataContext as OdClockViewModel)?.StartTicking();

    private void OnUnloaded(object sender, RoutedEventArgs e) => (DataContext as OdClockViewModel)?.StopTicking();
}
