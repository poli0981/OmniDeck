using Microsoft.Extensions.DependencyInjection;
using OmniDeck.Core.Modules;
using OmniDeck.Modules.Calculator;
using OmniDeck.Modules.Calendar;
using OmniDeck.Modules.Clock;
using OmniDeck.Modules.Media;
using OmniDeck.Modules.Stocks;
using OmniDeck.Modules.Timer;
using OmniDeck.Modules.Weather;

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

        // Events outlive the Calendar module's views, and reminders fire whether or not it was opened.
        services.AddSingleton<OdCalendarStore>();
        services.AddSingleton<OdReminderService>();

        services.AddSingleton<IOdModule, OdClockModule>();
        services.AddSingleton<IOdModule, OdTimerModule>();
        services.AddSingleton<IOdModule, OdCalculatorModule>();
        services.AddSingleton<IOdModule, OdWeatherModule>();
        services.AddSingleton<IOdModule, OdStocksModule>();
        services.AddSingleton<IOdModule, OdCalendarModule>();
        services.AddSingleton<IOdModule, OdMediaModule>();
        return services;
    }
}
