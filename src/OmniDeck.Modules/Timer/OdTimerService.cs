using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;

namespace OmniDeck.Modules.Timer;

/// <summary>
/// Runs the countdowns and the stopwatch apart from any view (docs/ARCHITECTURE.md §2: durable state
/// lives in services), so timers keep counting while the Timer module is hibernated or closed, and
/// the tray can start one without opening it. A finished countdown raises a notification. Timers run
/// only while OmniDeck does (docs/SPEC.md §3.1). Used from the UI thread; the alarm comes back to it.
/// </summary>
public sealed class OdTimerService : IOdQuickTimer, IDisposable
{
    private readonly TimeProvider _time;
    private readonly IOdNotifier _notifier;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly List<OdCountdown> _countdowns = [];
    private ITimer? _alarm;

    public OdTimerService(TimeProvider time, IOdNotifier notifier)
    {
        _time = time;
        _notifier = notifier;
    }

    /// <summary>Raised after any countdown or the stopwatch changes.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<OdCountdown> Countdowns => _countdowns;

    public OdStopwatch Stopwatch { get; } = new();

    public DateTimeOffset Now => _time.GetUtcNow();

    public OdCountdown Add(string name, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        var countdown = new OdCountdown(name, duration);
        _countdowns.Add(countdown);
        RaiseChanged();
        return countdown;
    }

    public OdCountdown StartQuick(TimeSpan duration)
    {
        var countdown = Add(OdLocalization.Get("Timer_QuickName"), duration);
        Start(countdown);
        return countdown;
    }

    /// <summary>Starts or resumes a countdown; a finished one starts over.</summary>
    public void Start(OdCountdown countdown)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        if (countdown.IsRunning)
        {
            return;
        }

        if (countdown.IsFinished)
        {
            countdown.IsFinished = false;
            countdown.Remaining = countdown.Duration;
        }

        countdown.EndsAt = Now + countdown.Remaining;
        Reschedule();
    }

    public void Pause(OdCountdown countdown)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        if (countdown.IsRunning)
        {
            countdown.Remaining = countdown.RemainingAt(Now);
            countdown.EndsAt = null;
            Reschedule();
        }
    }

    public void Reset(OdCountdown countdown)
    {
        ArgumentNullException.ThrowIfNull(countdown);
        countdown.EndsAt = null;
        countdown.Remaining = countdown.Duration;
        countdown.IsFinished = false;
        Reschedule();
    }

    public void Remove(OdCountdown countdown)
    {
        _countdowns.Remove(countdown);
        Reschedule();
    }

    public void StartStopwatch()
    {
        Stopwatch.Start(Now);
        RaiseChanged();
    }

    public void StopStopwatch()
    {
        Stopwatch.Stop(Now);
        RaiseChanged();
    }

    public void Lap()
    {
        Stopwatch.Lap(Now);
        RaiseChanged();
    }

    public void ResetStopwatch()
    {
        Stopwatch.Reset();
        RaiseChanged();
    }

    public void Dispose() => _alarm?.Dispose();

    /// <summary>One alarm, set for the countdown that ends first.</summary>
    private void Reschedule()
    {
        _alarm?.Dispose();
        _alarm = null;
        var next = _countdowns.Where(countdown => countdown.EndsAt is not null).Min(countdown => countdown.EndsAt);
        if (next is { } endsAt)
        {
            var due = endsAt - Now;
            _alarm = _time.CreateTimer(_ => OnUiThread(Finish), null, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        RaiseChanged();
    }

    private void Finish()
    {
        var now = Now;
        foreach (var countdown in _countdowns.Where(countdown => countdown.EndsAt <= now).ToList())
        {
            countdown.EndsAt = null;
            countdown.Remaining = TimeSpan.Zero;
            countdown.IsFinished = true;
            _notifier.Notify(OdLocalization.Get("Timer_DoneTitle"), OdLocalization.Format("Timer_DoneMessage", countdown.Name));
        }

        Reschedule();
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

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
