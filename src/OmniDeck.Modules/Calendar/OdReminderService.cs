using System.Globalization;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// Calendar reminders as notifications (docs/SPEC.md §3). The service lives as long as the app, not the
/// Calendar module, so reminders fire whether or not Calendar was opened, but only while OmniDeck runs:
/// one that fell due while it was closed is not shown later. Clicking a reminder opens Calendar.
/// </summary>
public sealed class OdReminderService : IDisposable
{
    /// <summary>The Calendar module's id, which reminders open.</summary>
    internal const string CalendarId = "calendar";

    // The editor offers reminders up to a day ahead; a week also covers most imported ones.
    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(8);

    private readonly OdCalendarStore _store;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private SynchronizationContext? _context;
    private ITimer? _alarm;
    private DateTime _shownUntil;
    private bool _started;

    public OdReminderService(OdCalendarStore store, TimeProvider time, IOdNotifier notifier)
    {
        _store = store;
        _time = time;
        _notifier = notifier;
    }

    private DateTime Now => _time.GetLocalNow().DateTime;

    /// <summary>Starts watching for reminders: once, at startup, on the UI thread.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _context = SynchronizationContext.Current;
        _shownUntil = Now;
        _store.Changed += OnStoreChanged;
        Reschedule();
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        _alarm?.Dispose();
    }

    /// <summary>The reminders due after <paramref name="after"/>, soonest first.</summary>
    internal IEnumerable<(DateTime At, OdOccurrence Occurrence)> Upcoming(DateTime after) =>
        _store.All()
            .Where(value => value.ReminderMinutes is not null)
            .SelectMany(value => OdRecurrence.Between(value, after, after + LookAhead))
            .Select(occurrence => (At: occurrence.Start.AddMinutes(-occurrence.Event.ReminderMinutes!.Value), Occurrence: occurrence))
            .Where(reminder => reminder.At > after)
            .OrderBy(reminder => reminder.At);

    private void OnStoreChanged(object? sender, EventArgs e) => Reschedule();

    private void Reschedule()
    {
        _alarm?.Dispose();
        var next = Upcoming(_shownUntil).Select(reminder => (DateTime?)reminder.At).FirstOrDefault();

        // With nothing within the look-ahead, look again before it runs out.
        var due = (next ?? Now + TimeSpan.FromDays(7)) - Now;
        _alarm = _time.CreateTimer(_ => OnUiThread(Fire), null, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void Fire()
    {
        var now = Now;
        foreach (var (_, occurrence) in Upcoming(_shownUntil).TakeWhile(reminder => reminder.At <= now).ToList())
        {
            var title = occurrence.Event.Title.Length > 0 ? occurrence.Event.Title : OdLocalization.Get("Calendar_Untitled");
            _notifier.Notify(new OdNotification(title, When(occurrence, now), CalendarId));
        }

        _shownUntil = now;
        Reschedule();
    }

    private static string When(OdOccurrence occurrence, DateTime now)
    {
        if (occurrence.Event.AllDay)
        {
            return OdLocalization.Format("Calendar_ReminderAllDay", occurrence.Start.ToString("D", CultureInfo.CurrentCulture));
        }

        var format = occurrence.Start.Date == now.Date ? "t" : "g";
        return OdLocalization.Format("Calendar_ReminderStarts", occurrence.Start.ToString(format, CultureInfo.CurrentCulture));
    }

    private void OnUiThread(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }
}
