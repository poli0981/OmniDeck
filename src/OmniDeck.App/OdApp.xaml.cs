using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Composition;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Legal;
using OmniDeck.App.Resources;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.App.Updates;
using OmniDeck.Core;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Legal;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Runtime;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Modules;
using OmniDeck.Modules.Resources;
using Serilog;

namespace OmniDeck.App;

/// <summary>
/// The WPF application: builds the data root, the logger and the service container, hooks crash
/// handling, then shows the main window. Shutdown is explicit because the tray keeps OmniDeck
/// running after its window closes.
/// </summary>
public partial class OdApp
{
    private readonly OdSingleInstanceGuard _guard;
    private ServiceProvider? _services;

    internal OdApp(OdSingleInstanceGuard guard)
    {
        _guard = guard;
    }

    /// <summary>The legal documents that ship next to the executable.</summary>
    public static string LegalFolder => Path.Combine(AppContext.BaseDirectory, "legal");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        OdLocalization.Instance.Register(OdAppStrings.Resources);
        OdLocalization.Instance.Register(OdModuleStrings.Resources);
        try
        {
            Start(startMinimized: e.Args.Contains(OdRunKeyStartup.MinimizedArgument, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.ForContext<OdApp>().Fatal(ex, "OmniDeck could not start");
            MessageBox.Show(OdLocalization.Format("Error_StartFailed", ex.Message), OdLocalization.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.ForContext<OdApp>().Information("OmniDeck exiting with code {ExitCode}", e.ApplicationExitCode);
        _services?.Dispose();
        OdDatabase.ReleaseAll();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void Start(bool startMinimized)
    {
        var dataRoot = OdDataRoot.Default;
        dataRoot.EnsureCreated();
        Log.Logger = OdLogging.CreateLogger(dataRoot);
        Log.ForContext<OdApp>().Information("OmniDeck {Version} starting", OdVersionInfo.FromAssembly(typeof(OdApp).Assembly).DisplayText);

        _services = new ServiceCollection()
            .AddOmniDeckCore(dataRoot, Log.Logger)
            .AddOmniDeckModules()
            .AddOmniDeckApp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _services.GetRequiredService<OdCrashHandler>().Hook(this);
        var applied = _services.GetRequiredService<OdDatabase>().Migrate(OdCoreMigrations.All);
        Log.ForContext<OdApp>().Information("Database ready ({Applied} migrations applied)", applied);

        var settings = _services.GetRequiredService<IOdSettingsService>();
        OdLocalization.Instance.SetCulture(OdLocalization.Instance.ResolveCulture(settings.Current.Language));
        OdLocalization.Instance.TrackWindowLanguage();
        settings.Changed += (_, e) => Dispatcher.Invoke(() => OdLocalization.Instance.SetCulture(OdLocalization.Instance.ResolveCulture(e.Settings.Language)));

        _services.GetRequiredService<OdThemeEngine>().Start(this);

        // The legal gate comes first, on the first run and after each legal update; Quit ends here.
        var legal = _services.GetRequiredService<OdLegalGate>();
        if (!legal.IsAcknowledged && !new OdLegalWindow(new OdLegalViewModel(legal, LegalFolder)).ShowGate())
        {
            Log.ForContext<OdApp>().Information("Legal documents not acknowledged; quitting");
            Shutdown(0);
            return;
        }

        var meta = _services.GetRequiredService<IOdAppMeta>();
        if (OdOnboardingViewModel.IsNeeded(meta))
        {
            new OdOnboardingWindow(new OdOnboardingViewModel(settings, meta)).ShowDialog();
        }

        // Only now may a second launch bring the window up: never past the gate.
        _guard.StartListening(() => _ = Dispatcher.InvokeAsync(() => MainShell().Present()));
        var window = MainShell();
        if (!startMinimized)
        {
            window.Present();
        }

        _services.GetRequiredService<OdRamGovernor>().Start();
        _services.GetRequiredService<OdUpdateWatcher>().Start();
    }

    /// <summary>The main window, created once. It owns the tray icon, so it exists even while hidden.</summary>
    private OdMainWindow MainShell()
    {
        if (MainWindow is not OdMainWindow window)
        {
            window = _services!.GetRequiredService<OdMainWindow>();
            MainWindow = window;

            // A real close (tray off, or Quit) ends the app; otherwise closing only hides the window.
            var lifetime = _services!.GetRequiredService<IOdAppLifetime>();
            window.Closed += (_, _) =>
            {
                if (!lifetime.IsQuitting)
                {
                    lifetime.Quit();
                }
            };
        }

        return window;
    }
}
