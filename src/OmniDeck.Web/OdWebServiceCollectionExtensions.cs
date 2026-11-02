using Microsoft.Extensions.DependencyInjection;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Modules;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Runtime;

namespace OmniDeck.Web;

/// <summary>Registers the web runner: its services, and one module for each app in the session's catalog.</summary>
public static class OdWebServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckWeb(this IServiceCollection services, OdCatalog catalog, OdWebOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);
        services.AddSingleton(catalog);
        services.AddSingleton(options);
        services.AddSingleton<OdCatalogService>();
        services.AddSingleton<OdWebRuntime>();
        services.AddSingleton<IOdMemoryReporter, OdWebMemoryReporter>();
        services.AddSingleton<OdSignInHealth>();

        // After the built-in modules, in catalog order: that is the library's order.
        foreach (var entry in catalog.Apps)
        {
            services.AddSingleton<IOdModule>(provider => new OdWebAppModule(
                entry,
                catalog,
                provider.GetRequiredService<OdWebRuntime>(),
                provider.GetRequiredService<OdSignInHealth>(),
                provider.GetRequiredService<TimeProvider>()));
        }

        return services;
    }
}
