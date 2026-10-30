using System.Windows;
using OmniDeck.Core.Modules;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Clock;

/// <summary>The world clock module. Its zones live in the settings document, so hibernation keeps nothing else.</summary>
public sealed class OdClockModule : IOdModule
{
    private IOdModuleContext? _context;
    private OdClockViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new("clock", "Clock_Name", nameof(SymbolRegular.Clock24), "Category_Time", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var context = _context ?? throw new InvalidOperationException("The host initializes a module before its first view.");
        var settings = context.Settings.Get(OdModulesJsonContext.Default.OdClockSettings) ?? new OdClockSettings();
        _viewModel = new OdClockViewModel(settings, saved => context.Settings.Set(saved, OdModulesJsonContext.Default.OdClockSettings), context.Time);
        return new OdClockView { DataContext = _viewModel };
    }

    public Task ActivateAsync() => Task.CompletedTask;

    public Task<bool> CanSuspendAsync() => Task.FromResult(true);

    public Task SuspendAsync(OdSuspendReason reason)
    {
        _viewModel?.StopTicking();
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
