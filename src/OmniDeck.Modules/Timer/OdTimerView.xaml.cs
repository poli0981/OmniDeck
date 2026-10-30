using System.Windows;

namespace OmniDeck.Modules.Timer;

/// <summary>Countdowns and the stopwatch; the texts refresh only while the view is on screen.</summary>
public partial class OdTimerView
{
    public OdTimerView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => (DataContext as OdTimerViewModel)?.StartTicking();

    private void OnUnloaded(object sender, RoutedEventArgs e) => (DataContext as OdTimerViewModel)?.StopTicking();
}
