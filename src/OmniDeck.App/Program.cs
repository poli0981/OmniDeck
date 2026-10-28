using Velopack;

namespace OmniDeck.App;

/// <summary>
/// Process entry point. Velopack's install/update/uninstall hooks run first (they may exit the
/// process and must not wait for WPF to load OdApp.xaml resources); only then is the WPF
/// <see cref="OdApp"/> created.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        VelopackApp.Build().Run();

        var app = new OdApp();
        app.InitializeComponent();
        return app.Run();
    }
}
