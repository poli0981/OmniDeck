using System.Diagnostics;
using System.Windows.Navigation;

namespace OmniDeck.Modules.Weather;

/// <summary>The weather screen, bound to <see cref="OdWeatherViewModel"/>.</summary>
public partial class OdWeatherView
{
    public OdWeatherView()
    {
        InitializeComponent();
    }

    // Open-Meteo's terms ask for an attribution that links to its site; it opens in the default browser.
    private void OnAttributionNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        e.Handled = true;
    }
}
