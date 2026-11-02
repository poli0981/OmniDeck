using System.Windows;
using OmniDeck.Core.Modules;
using Wpf.Ui.Controls;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// The calendar module (docs/SPEC.md §3): local events with repeats and reminders, and .ics import and
/// export. Events live in <see cref="OdCalendarStore"/> and reminders in <see cref="OdReminderService"/>,
/// so the module can always be suspended or hibernated.
/// </summary>
public sealed class OdCalendarModule(OdCalendarStore store) : IOdModule
{
    private IOdModuleContext? _context;
    private OdCalendarViewModel? _viewModel;

    public OdModuleInfo Info { get; } = new(OdReminderService.CalendarId, "Calendar_Name", nameof(SymbolRegular.CalendarLtr24), "Category_Time", OdRamClass.Light);

    public Task InitializeAsync(IOdModuleContext context)
    {
        _context = context;
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        var context = _context ?? throw new InvalidOperationException("The host initializes a module before its first view.");
        _viewModel = new OdCalendarViewModel(store, context.Time);
        return new OdCalendarView { DataContext = _viewModel };
    }

    public Task ActivateAsync()
    {
        // A day may have passed since the module was last on screen.
        _viewModel?.Refresh();
        return Task.CompletedTask;
    }

    public Task<bool> CanSuspendAsync() => Task.FromResult(true);

    public Task SuspendAsync(OdSuspendReason reason) => Task.CompletedTask;

    public Task HibernateAsync()
    {
        _viewModel?.Dispose();
        _viewModel = null;
        return Task.CompletedTask;
    }

    public Task CloseAsync() => HibernateAsync();
}
