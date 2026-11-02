using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.App.Updates;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Runtime;
using Serilog;
using Wpf.Ui;

namespace OmniDeck.App.Composition;

/// <summary>Registers the WPF shell's services on top of <c>AddOmniDeckCore</c>.</summary>
internal static class OdAppServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckApp(this IServiceCollection services)
    {
        // WPF-UI's dialog and snackbar services; the main window hands them their hosts.
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<OdDialogs>();

        services.AddSingleton<OdMessageBoxErrorPresenter>();
        services.AddSingleton<IOdErrorPresenter, OdDialogErrorPresenter>();
        services.AddSingleton<OdCrashHandler>();
        services.AddSingleton<OdThemeEngine>();

        services.AddSingleton<OdTrayNotifier>();
        services.AddSingleton<IOdNotifier>(provider => provider.GetRequiredService<OdTrayNotifier>());
        services.AddSingleton<IOdAppLifetime, OdAppLifetime>();
        services.AddSingleton<OdShellViewModel>();
        services.AddSingleton<OdMemoryViewModel>();
        services.AddSingleton<IOdUpdateService, OdVelopackUpdateService>();
        services.AddSingleton<OdUpdateWatcher>();
        services.AddSingleton<IOdStartupRegistration>(_ => new OdRunKeyStartup());
        services.AddSingleton(provider => new OdAboutViewModel(provider.GetRequiredService<IOdUpdateService>(), OdExternal.Open));
        services.AddSingleton(provider => new OdWebAppsViewModel(
            provider.GetRequiredService<OdCatalogService>(),
            provider.GetRequiredService<OdWebPreferences>(),
            provider.GetRequiredService<IOdSettingsService>(),
            provider.GetRequiredService<OdModuleHost>(),
            provider.GetRequiredService<OdSignInHealth>(),
            profile => provider.GetRequiredService<OdWebRuntime>().ClearProfileAsync(profile, MainWindowHandle()),
            apps => provider.GetRequiredService<OdDialogs>().ConfirmAsync(
                OdLocalization.Get("WebApps_ClearTitle"),
                OdLocalization.Format("WebApps_ClearMessage", apps),
                OdLocalization.Get("WebApps_ClearConfirm")),
            provider.GetRequiredService<ILogger>()));
        services.AddSingleton(provider => new OdSettingsViewModel(
            provider.GetRequiredService<IOdSettingsService>(),
            provider.GetRequiredService<IOdStartupRegistration>(),
            provider.GetRequiredService<OdDataRoot>(),
            provider.GetRequiredService<OdMemoryViewModel>(),
            provider.GetRequiredService<OdWebAppsViewModel>(),
            provider.GetRequiredService<OdAboutViewModel>(),
            OdExternal.OpenFolder));
        services.AddSingleton<OdMainWindow>();
        return services;
    }

    // The hidden browser that clears a profile needs a window; Settings is in the main one.
    private static nint MainWindowHandle() => new WindowInteropHelper(Application.Current.MainWindow).Handle;
}
