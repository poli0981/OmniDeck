using Microsoft.Extensions.DependencyInjection;
using OmniDeck.Core.Modules;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Timer;

namespace OmniDeck.Modules;

/// <summary>Registers the built-in modules; their order here is the library's order.</summary>
public static class OdModulesServiceCollectionExtensions
{
    public static IServiceCollection AddOmniDeckModules(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The timer service outlives the Timer module's views, and the tray starts quick timers on it.
        services.AddSingleton<OdTimerService>();
        services.AddSingleton<IOdQuickTimer>(provider => provider.GetRequiredService<OdTimerService>());

        services.AddSingleton<IOdModule, OdClockModule>();
        services.AddSingleton<IOdModule, OdTimerModule>();
        services.AddSingleton<IOdModule, OdCalculatorModule>();
        return services;
    }
}
