using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App.Diagnostics;

namespace OmniDeck.App.Composition;

/// <summary>Registers the WPF shell's services on top of <c>AddOmniDeckCore</c>.</summary>
internal static class OdAppServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckApp(this IServiceCollection services)
    {
        services.AddSingleton<IOdErrorPresenter, OdMessageBoxErrorPresenter>();
        services.AddSingleton<OdCrashHandler>();
        services.AddSingleton<OdMainWindow>();
        return services;
    }
}
