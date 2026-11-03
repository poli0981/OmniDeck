using System.Globalization;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Timer;

/// <summary>
/// Alarms (docs/SPEC.md §3.1). Like the timers, they live as long as the app, not the Timer module, so
/// they ring whether or not Timer is open, as a notification that opens Timer, but only while OmniDeck
/// runs: one that came due while it was closed is not shown later, and one the PC slept through rings on
/// waking only within the hour. A one-off alarm turns itself off once it rang. Used from the UI thread.
/// </summary>
public sealed class OdAlarmService : IDisposable
{
    /// <summary>Where the alarms are kept: a settings document of their own, beside the Timer module's.</summary>
    internal const string DocumentId = "timer-alarms";

    private static readonly TimeSpan Late = TimeSpan.FromHours(1);

    private readonly OdModuleSettingsStore _store;
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private List<OdAlarm>? _loaded;
    private SynchronizationContext? _context;
    private ITimer? _timer;
    private DateTime _rangUntil;
    private bool _started;

    public OdAlarmService(OdDatabase database, TimeProvider time, IOdNotifier notifier)
    {
        _store = new OdModuleSettingsStore(database, DocumentId);
        _time = time;
        _notifier = notifier;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<OdAlarm> Alarms => Items;

    private DateTime Now => _time.GetLocalNow().DateTime;

    // Read on first use, not when the container builds the service, so nothing opens the database early.
    private List<OdAlarm> Items => _loaded ??= [.. _store.Get(OdModulesJsonContext.Default.OdAlarmsDocument)?.Alarms ?? []];

    /// <summary>Starts ringing: once, at startup, on the UI thread.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _context = SynchronizationContext.Current;
        _rangUntil = Now;
        Reschedule();
    }

    /// <summary>When <paramref name="alarm"/> rings next; null when it is off.</summary>
    public DateTime? NextAt(OdAlarm alarm) => OdAlarmSchedule.NextAt(alarm, Now, _time.LocalTimeZone);

    public OdAlarm Add(TimeOnly time, OdAlarmRepeat repeat, string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var alarm = new OdAlarm(Guid.NewGuid().ToString("N"), time, repeat, label.Trim(), IsOn: true, Now);
        Items.Add(alarm);
        Save();
        return alarm;
    }

    public void SetOn(string id, bool on) => Replace(id, alarm => alarm.IsOn == on ? alarm : alarm with { IsOn = on, ArmedAt = Now });

    public void Remove(string id)
    {
        if (Items.RemoveAll(alarm => string.Equals(alarm.Id, id, StringComparison.Ordinal)) > 0)
        {
            Save();
        }
    }

    public void Dispose() => _timer?.Dispose();

    private void Replace(string id, Func<OdAlarm, OdAlarm> change)
    {
        var index = Items.FindIndex(alarm => string.Equals(alarm.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var changed = change(Items[index]);
        if (changed != Items[index])
        {
            Items[index] = changed;
            Save();
        }
    }

    private void Save()
    {
        _store.Set(new OdAlarmsDocument([.. Items]), OdModulesJsonContext.Default.OdAlarmsDocument);
        Changed?.Invoke(this, EventArgs.Empty);
        if (_started)
        {
            Reschedule();
        }
    }

    private void Reschedule()
    {
        _timer?.Dispose();
        var next = Items.Select(alarm => OdAlarmSchedule.NextAt(alarm, _rangUntil, _time.LocalTimeZone)).Where(at => at is not null).Min();

        // With nothing set, look again in a day: the clock may have moved, and with it the next alarm.
        var due = (next ?? Now.AddDays(1)) - Now;
        _timer = _time.CreateTimer(_ => OnUiThread(Ring), null, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void Ring()
    {
        var now = Now;
        var rang = false;
        foreach (var alarm in Items.ToList())
        {
            if (OdAlarmSchedule.NextAt(alarm, _rangUntil, _time.LocalTimeZone) is not { } at || at > now)
            {
                continue;
            }

            if (now - at <= Late)
            {
                var body = alarm.Label.Length > 0
                    ? OdLocalization.Format("Timer_AlarmLabelled", alarm.Label, alarm.Time.ToString("t", CultureInfo.CurrentCulture))
                    : alarm.Time.ToString("t", CultureInfo.CurrentCulture);
                _notifier.Notify(new OdNotification(OdLocalization.Get("Timer_AlarmTitle"), body, OdTimerModule.Id));
            }

            if (alarm.Repeat == OdAlarmRepeat.Once)
            {
                var index = Items.FindIndex(item => string.Equals(item.Id, alarm.Id, StringComparison.Ordinal));
                Items[index] = alarm with { IsOn = false };
                rang = true;
            }
        }

        _rangUntil = now;
        if (rang)
        {
            Save();
        }
        else
        {
            Reschedule();
        }
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
