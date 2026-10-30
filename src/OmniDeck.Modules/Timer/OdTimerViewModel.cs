using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>
/// The Timer view's state over <see cref="OdTimerService"/>: countdowns with their remaining time,
/// the add form and presets, and the stopwatch with its laps. It holds no timing of its own, so
/// dropping it on hibernation loses nothing. Used from the UI thread.
/// </summary>
public sealed partial class OdTimerViewModel : ObservableObject, IDisposable
{
    /// <summary>How often the view's texts refresh while it is visible: fine enough for hundredths.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(50);

    private readonly OdTimerService _service;
    private readonly TimeProvider _time;
    private ITimer? _ticker;

    // Doubles because WPF-UI's NumberBox edits a double?; whole numbers are taken from them.
    [ObservableProperty]
    private double? _newMinutes = 5;

    [ObservableProperty]
    private double? _newSeconds = 0;

    [ObservableProperty]
    private string _newName = "";

    [ObservableProperty]
    private string _stopwatchText = OdDurationText.FormatPrecise(TimeSpan.Zero);

    [ObservableProperty]
    private bool _stopwatchRunning;

    [ObservableProperty]
    private string _stopwatchToggleText = "";

    [ObservableProperty]
    private bool _hasNoCountdowns = true;

    public OdTimerViewModel(OdTimerService service, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _time = time;
        service.Changed += OnServiceChanged;
        Sync();
    }

    public ObservableCollection<OdCountdownViewModel> Countdowns { get; } = [];

    /// <summary>The one-click durations.</summary>
    public IReadOnlyList<OdTimerPreset> Presets { get; } =
        [.. new[] { 1, 5, 10, 25 }.Select(minutes => new OdTimerPreset(minutes, OdLocalization.Format("Timer_PresetMinutes", minutes)))];

    /// <summary>Laps, newest first.</summary>
    public ObservableCollection<OdLapViewModel> Laps { get; } = [];

    /// <summary>Refreshes the texts on the calling (UI) thread until <see cref="StopTicking"/>.</summary>
    public void StartTicking()
    {
        if (_ticker is not null)
        {
            return;
        }

        var context = SynchronizationContext.Current;
        _ticker = _time.CreateTimer(_ => context?.Post(_ => Refresh(), null), null, RefreshInterval, RefreshInterval);
        Refresh();
    }

    public void StopTicking()
    {
        _ticker?.Dispose();
        _ticker = null;
    }

    public void Refresh()
    {
        var now = _service.Now;
        foreach (var countdown in Countdowns)
        {
            countdown.Update(now);
        }

        StopwatchText = OdDurationText.FormatPrecise(_service.Stopwatch.ElapsedAt(now));
        StopwatchRunning = _service.Stopwatch.IsRunning;
        StopwatchToggleText = StopwatchRunning ? OdLocalization.Get("Timer_Stop") : OdLocalization.Get("Timer_Start");
    }

    public void Dispose()
    {
        StopTicking();
        _service.Changed -= OnServiceChanged;
    }

    /// <summary>Adds a countdown from the form and starts it.</summary>
    [RelayCommand]
    private void AddCountdown()
    {
        var duration = TimeSpan.FromMinutes(Math.Max(Math.Floor(NewMinutes ?? 0), 0)) + TimeSpan.FromSeconds(Math.Max(Math.Floor(NewSeconds ?? 0), 0));
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewName)
            ? OdLocalization.Format("Timer_DefaultName", OdDurationText.Format(duration))
            : NewName.Trim();
        _service.Start(_service.Add(name, duration));
        NewName = "";
    }

    /// <summary>Starts a countdown of <paramref name="minutes"/> minutes, as the preset buttons do.</summary>
    [RelayCommand]
    private void AddPreset(int minutes)
    {
        var duration = TimeSpan.FromMinutes(minutes);
        _service.Start(_service.Add(OdLocalization.Format("Timer_DefaultName", OdDurationText.Format(duration)), duration));
    }

    [RelayCommand]
    private void Toggle(OdCountdownViewModel countdown)
    {
        if (countdown.Countdown.IsRunning)
        {
            _service.Pause(countdown.Countdown);
        }
        else
        {
            _service.Start(countdown.Countdown);
        }
    }

    [RelayCommand]
    private void Reset(OdCountdownViewModel countdown) => _service.Reset(countdown.Countdown);

    [RelayCommand]
    private void Delete(OdCountdownViewModel countdown) => _service.Remove(countdown.Countdown);

    [RelayCommand]
    private void ToggleStopwatch()
    {
        if (_service.Stopwatch.IsRunning)
        {
            _service.StopStopwatch();
        }
        else
        {
            _service.StartStopwatch();
        }
    }

    [RelayCommand]
    private void Lap() => _service.Lap();

    [RelayCommand]
    private void ResetStopwatch() => _service.ResetStopwatch();

    private void OnServiceChanged(object? sender, EventArgs e) => Sync();

    /// <summary>Mirrors the service's countdowns and laps, then refreshes the texts.</summary>
    private void Sync()
    {
        foreach (var gone in Countdowns.Where(row => !_service.Countdowns.Contains(row.Countdown)).ToList())
        {
            Countdowns.Remove(gone);
        }

        foreach (var added in _service.Countdowns.Where(countdown => Countdowns.All(row => row.Countdown != countdown)))
        {
            Countdowns.Add(new OdCountdownViewModel(added));
        }

        HasNoCountdowns = Countdowns.Count == 0;
        var laps = _service.Stopwatch.Laps;
        if (laps.Count != Laps.Count)
        {
            Laps.Clear();
            for (var i = laps.Count - 1; i >= 0; i--)
            {
                var length = laps[i] - (i > 0 ? laps[i - 1] : TimeSpan.Zero);
                Laps.Add(new OdLapViewModel(OdLocalization.Format("Timer_LapNumber", i + 1), OdDurationText.FormatPrecise(length), OdDurationText.FormatPrecise(laps[i])));
            }
        }

        Refresh();
    }
}
