using OmniDeck.Core.Runtime;
using OmniDeck.Core.Storage;
using Velopack;

namespace OmniDeck.App;

/// <summary>
/// Process entry point. Velopack's install/update/uninstall hooks run first (they may exit the
/// process and must not wait for WPF to load OdApp.xaml resources); then a second OmniDeck hands off
/// to the first one and exits; only then is the WPF <see cref="OdApp"/> created.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        VelopackApp.Build()
            // The privacy policy promises that uninstalling removes everything OmniDeck stored.
            .OnBeforeUninstallFastCallback(_ => OdDataRoot.Default.TryDelete())
            .Run();

        var dataRoot = DataRoot();
        using var guard = new OdSingleInstanceGuard(dataRoot.InstanceSuffix);
        if (!guard.IsPrimaryInstance)
        {
            guard.SignalPrimaryInstance();
            return 0;
        }

        var app = new OdApp(guard, dataRoot);
        app.InitializeComponent();
        return app.Run();
    }

    /// <summary>
    /// The signed-in user's data root. A Debug build can be pointed elsewhere with
    /// <see cref="OdDataRoot.OverrideVariable"/>, so it runs beside an installed OmniDeck without
    /// touching its data; a Release build never reads the variable.
    /// </summary>
    private static OdDataRoot DataRoot()
    {
#if DEBUG
        return OdDataRoot.Choose(Environment.GetEnvironmentVariable(OdDataRoot.OverrideVariable));
#else
        return OdDataRoot.Default;
#endif
    }
}
