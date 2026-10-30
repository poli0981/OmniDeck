using System.Runtime.InteropServices;
using System.Windows;
using OmniDeck.Core.Modules;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Calculator;

/// <summary>
/// The Calculator module. The view model holds the session's history, so hibernation drops only the
/// view; closing the module starts a new session.
/// </summary>
public sealed class OdCalculatorModule : IOdModule
{
    private OdCalculatorViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new("calculator", "Calc_Name", nameof(SymbolRegular.Calculator24), "Category_Tools", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.Get(OdModulesJsonContext.Default.OdCalculatorSettings) ?? new OdCalculatorSettings();
        _viewModel = new OdCalculatorViewModel(settings, saved => context.Settings.Set(saved, OdModulesJsonContext.Default.OdCalculatorSettings), Copy);
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView() => new OdCalculatorView
    {
        DataContext = _viewModel ?? throw new InvalidOperationException("The host initializes a module before its first view."),
    };

    public Task ActivateAsync() => Task.CompletedTask;

    public Task<bool> CanSuspendAsync() => Task.FromResult(true);

    public Task SuspendAsync(OdSuspendReason reason) => Task.CompletedTask;

    public Task HibernateAsync() => Task.CompletedTask;

    public Task CloseAsync()
    {
        _viewModel?.Reset();
        return Task.CompletedTask;
    }

    private static void Copy(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            // Another app holds the clipboard; the result stays on screen to copy again.
        }
    }
}
