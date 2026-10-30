using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Composition;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Theming;
using OmniDeck.Core;
using OmniDeck.Core.Runtime;
using OmniDeck.Core.Storage;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            Start();
        }
        catch (Exception ex)
        {
            Log.ForContext<OdApp>().Fatal(ex, "OmniDeck could not start");
            MessageBox.Show($"OmniDeck could not start.\n\n{ex.Message}", "OmniDeck", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void Start()
    {
        var dataRoot = OdDataRoot.Default;
        dataRoot.EnsureCreated();
        Log.Logger = OdLogging.CreateLogger(dataRoot);
        Log.ForContext<OdApp>().Information("OmniDeck {Version} starting", OdVersionInfo.FromAssembly(typeof(OdApp).Assembly).DisplayText);

        _services = new ServiceCollection()
            .AddOmniDeckCore(dataRoot, Log.Logger)
            .AddOmniDeckApp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _services.GetRequiredService<OdCrashHandler>().Hook(this);
        var applied = _services.GetRequiredService<OdDatabase>().Migrate(OdCoreMigrations.All);
        Log.ForContext<OdApp>().Information("Database ready ({Applied} migrations applied)", applied);
        _services.GetRequiredService<OdThemeEngine>().Start(this);
        _guard.StartListening(() => _ = Dispatcher.InvokeAsync(ShowMainWindow));
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (MainWindow is not OdMainWindow window)
        {
            window = _services!.GetRequiredService<OdMainWindow>();
            MainWindow = window;

            // Until the tray arrives (P1 WP4), closing the window ends the app.
            window.Closed += (_, _) => Shutdown();
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
