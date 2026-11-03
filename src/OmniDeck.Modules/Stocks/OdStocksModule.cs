using System.Windows;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Security;
using OmniDeck.Core.Storage;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The Stocks module (docs/SPEC.md §3.5): a watchlist of Twelve Data quotes with the user's own key.
/// It is Light, since hibernating it frees little and reopening costs no credits: the watchlist lives
/// in the settings document and the figures in the cache. It refreshes only while on screen.
/// </summary>
public sealed class OdStocksModule(IOdSecretStore secrets) : IOdModule
{
    private IOdModuleContext? _context;
    private OdStocksViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new("stocks", "Stocks_Name", nameof(SymbolRegular.ArrowTrending24), "Category_Info", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var context = _context ?? throw new InvalidOperationException("The host initializes a module before its first view.");
        var settings = context.Settings.Get(OdModulesJsonContext.Default.OdStocksSettings) ?? new OdStocksSettings();
        _viewModel = new OdStocksViewModel(
            settings,
            saved => context.Settings.Set(saved, OdModulesJsonContext.Default.OdStocksSettings),
            secrets,
            new OdTwelveData(context.Http, context.Log),
            new OdStocksCache(context.Database),
            new OdCreditLimiter(new OdAppMeta(context.Database), context.Time),
            context.Time,
            context.Log);
        return new OdStocksView { DataContext = _viewModel };
    }

    public Task ActivateAsync()
    {
        _viewModel?.Activate();
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        _viewModel?.Deactivate();
        return Task.CompletedTask;
    }

    public Task<bool> CanSuspendAsync() => Task.FromResult(true);

    public Task SuspendAsync(OdSuspendReason reason) => DeactivateAsync();

    public Task HibernateAsync()
    {
        _viewModel?.Dispose();
        _viewModel = null;
        return Task.CompletedTask;
    }

    public Task CloseAsync() => HibernateAsync();
}
