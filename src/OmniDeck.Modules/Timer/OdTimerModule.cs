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
    /// <summary>The module's id, which its notifications open when clicked.</summary>
    internal const string Id = "timer";

    private TimeProvider _time = TimeProvider.System;
    private OdTimerViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new(Id, "Timer_Name", nameof(SymbolRegular.Timer24), "Category_Time", OdRamClass.Light);

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

    /// <summary>
    /// Ticks again after a suspension. The view's Loaded event does that when it comes back on stage, but a
    /// window that was minimized never unloaded it.
    /// </summary>
    public Task ActivateAsync()
    {
        _viewModel?.StartTicking();
        return Task.CompletedTask;
    }

    /// <summary>Off screen there is nothing to show the time to.</summary>
    public Task DeactivateAsync()
    {
        _viewModel?.StopTicking();
        return Task.CompletedTask;
    }

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
