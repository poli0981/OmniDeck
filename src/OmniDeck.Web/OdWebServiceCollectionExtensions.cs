using Microsoft.Extensions.DependencyInjection;
using OmniDeck.Web.Catalog;

namespace OmniDeck.Web;

/// <summary>Registers the web runner's services.</summary>
public static class OdWebServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckWeb(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<OdCatalogService>();
        return services;
    }
}
