using System.Diagnostics;
using System.Windows.Navigation;

namespace OmniDeck.Modules.Stocks;

/// <summary>The Stocks screen, bound to <see cref="OdStocksViewModel"/>.</summary>
public partial class OdStocksView
{
    public OdStocksView()
    {
        InitializeComponent();
    }

    // Twelve Data's sign-up page and its attribution open in the default browser.
    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        e.Handled = true;
    }
}
