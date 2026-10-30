using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Settings;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.App.Updates;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
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
        services.AddSingleton<IOdStartupRegistration>(_ => new OdRunKeyStartup());
        services.AddSingleton(provider => new OdAboutViewModel(provider.GetRequiredService<IOdUpdateService>(), OdExternal.Open));
        services.AddSingleton(provider => new OdSettingsViewModel(
            provider.GetRequiredService<IOdSettingsService>(),
            provider.GetRequiredService<IOdStartupRegistration>(),
            provider.GetRequiredService<OdDataRoot>(),
            provider.GetRequiredService<OdMemoryViewModel>(),
            provider.GetRequiredService<OdAboutViewModel>(),
            OdExternal.OpenFolder));
        services.AddSingleton<OdMainWindow>();
        return services;
    }
}
