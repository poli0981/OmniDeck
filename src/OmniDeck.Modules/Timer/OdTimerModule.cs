using System.Windows;
using OmniDeck.Core.Modules;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Timer;

/// <summary>
/// The Timer module. Countdowns and the stopwatch live in <see cref="OdTimerService"/>, so the module
/// can always be suspended or hibernated, and closing it leaves running timers running.
/// </summary>
public sealed class OdTimerModule(OdTimerService service) : IOdModule
{
    private TimeProvider _time = TimeProvider.System;
    private OdTimerViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new("timer", "Timer_Name", nameof(SymbolRegular.Timer24), "Category_Time", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _time = context.Time;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        _viewModel = new OdTimerViewModel(service, _time);
        return new OdTimerView { DataContext = _viewModel };
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
