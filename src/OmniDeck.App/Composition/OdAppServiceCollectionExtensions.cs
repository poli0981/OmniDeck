using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Shell;
using OmniDeck.App.Theming;
using OmniDeck.Core.Modules;
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
        services.AddSingleton<OdMainWindow>();
        return services;
    }
}
