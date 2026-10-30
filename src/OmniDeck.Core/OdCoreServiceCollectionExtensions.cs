using Microsoft.Extensions.DependencyInjection;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;

namespace OmniDeck.Core;

/// <summary>Registers the UI-independent services. The app adds its own with <c>AddOmniDeckApp</c>.</summary>
public static class OdCoreServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckCore(this IServiceCollection services, OdDataRoot dataRoot, Serilog.ILogger log)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(dataRoot);
        services.AddSingleton(log);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOdSettingsService, OdSettingsService>();
        services.AddSingleton<OdDatabase>();
        services.AddSingleton<IOdAppMeta, OdAppMeta>();
        return services;
    }
}
