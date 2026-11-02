using System.Windows;
using OmniDeck.Core.Modules;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Weather;

/// <summary>
/// The weather module (docs/SPEC.md §3): forecasts from Open-Meteo for the places the user saves.
/// Places and units live in the settings document and forecasts in the cache, so hibernating it
/// loses nothing, and it touches the network only while it is on screen.
/// </summary>
public sealed class OdWeatherModule : IOdModule
{
    private IOdModuleContext? _context;
    private OdWeatherViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new("weather", "Weather_Name", nameof(SymbolRegular.WeatherPartlyCloudyDay24), "Category_Info", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var context = _context ?? throw new InvalidOperationException("The host initializes a module before its first view.");
        var settings = context.Settings.Get(OdModulesJsonContext.Default.OdWeatherSettings) ?? new OdWeatherSettings();
        var service = new OdWeatherService(context.Http, new OdWeatherCache(context.Database), context.Time, context.Log);
        _viewModel = new OdWeatherViewModel(settings, saved => context.Settings.Set(saved, OdModulesJsonContext.Default.OdWeatherSettings), service, context.Time);
        return new OdWeatherView { DataContext = _viewModel };
    }

    public Task ActivateAsync()
    {
        // Not awaited: showing the module must not wait on the network. The cache answers while it is
        // fresh, so this asks Open-Meteo at most every half hour per place.
        if (_viewModel is { } viewModel)
        {
            _ = viewModel.ShowAsync();
        }

        return Task.CompletedTask;
    }

    public Task<bool> CanSuspendAsync() => Task.FromResult(true);

    public Task SuspendAsync(OdSuspendReason reason)
    {
        _viewModel?.Cancel();
        return Task.CompletedTask;
    }

    public Task HibernateAsync()
    {
        _viewModel?.Dispose();
        _viewModel = null;
        return Task.CompletedTask;
    }

    public Task CloseAsync() => HibernateAsync();
}
