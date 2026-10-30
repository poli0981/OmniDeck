namespace OmniDeck.Modules.Timer;

/// <summary>The stopwatch: elapsed time banked across starts and stops, with laps.</summary>
public sealed class OdStopwatch
{
    private readonly List<TimeSpan> _laps = [];
    private DateTimeOffset? _startedAt;
    private TimeSpan _banked;

    public bool IsRunning => _startedAt is not null;

    /// <summary>The elapsed time at each lap, oldest first.</summary>
    public IReadOnlyList<TimeSpan> Laps => _laps;

    public TimeSpan ElapsedAt(DateTimeOffset now) => _banked + (_startedAt is { } started ? now - started : TimeSpan.Zero);

    public void Start(DateTimeOffset now) => _startedAt ??= now;

    public void Stop(DateTimeOffset now)
    {
        if (_startedAt is { } started)
        {
            _banked += now - started;
            _startedAt = null;
        }
    }

    public void Lap(DateTimeOffset now)
    {
        if (IsRunning)
        {
            _laps.Add(ElapsedAt(now));
        }
    }

    public void Reset()
    {
        _startedAt = null;
        _banked = TimeSpan.Zero;
        _laps.Clear();
    }
}
