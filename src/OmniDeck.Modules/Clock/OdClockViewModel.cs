using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OmniDeck.Modules.Clock;

/// <summary>
/// The world clock (docs/SPEC.md §3.1): local time first, then the zones the user added, in 12- or
/// 24-hour time. Zones and the hour format are saved through the module's settings document. Used
/// from the UI thread; it ticks only while its view is on screen.
/// </summary>
public sealed partial class OdClockViewModel : ObservableObject, IDisposable
{
    private readonly Action<OdClockSettings> _save;
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _local;
    private ITimer? _ticker;

    [ObservableProperty]
    private TimeZoneInfo? _selectedZone;

    [ObservableProperty]
    private bool _use24Hour;

    public OdClockViewModel(OdClockSettings settings, Action<OdClockSettings> save, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _save = save;
        _time = time;
        _local = time.LocalTimeZone;
        _use24Hour = settings.Use24Hour ?? !CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('h', StringComparison.Ordinal);
        Clocks.Add(new OdWorldClockViewModel(_local, isLocal: true));

        // A zone Windows no longer knows is skipped; it stays saved until the list changes.
        foreach (var id in settings.Zones)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            {
                Clocks.Add(new OdWorldClockViewModel(zone, isLocal: false));
            }
        }

        Tick();
    }

    public ObservableCollection<OdWorldClockViewModel> Clocks { get; } = [];

    /// <summary>The zones that can be added, in Windows' order (by UTC offset).</summary>
    public IReadOnlyList<TimeZoneInfo> AvailableZones { get; } = TimeZoneInfo.GetSystemTimeZones();

    public void StartTicking()
    {
        if (_ticker is not null)
        {
            return;
        }

        var context = SynchronizationContext.Current;
        var untilNextSecond = TimeSpan.FromMilliseconds(1000 - _time.GetUtcNow().Millisecond);
        _ticker = _time.CreateTimer(_ => context?.Post(_ => Tick(), null), null, untilNextSecond, TimeSpan.FromSeconds(1));
        Tick();
    }

    public void StopTicking()
    {
        _ticker?.Dispose();
        _ticker = null;
    }

    public void Tick()
    {
        var now = _time.GetUtcNow();
        foreach (var clock in Clocks)
        {
            clock.Update(now, _local, Use24Hour);
        }
    }

    public void Dispose() => StopTicking();

    [RelayCommand]
    private void AddZone()
    {
        if (SelectedZone is not { } zone || Clocks.Any(clock => !clock.IsLocal && string.Equals(clock.Zone.Id, zone.Id, StringComparison.Ordinal)))
        {
            return;
        }

        Clocks.Add(new OdWorldClockViewModel(zone, isLocal: false));
        Save();
        Tick();
    }

    [RelayCommand]
    private void RemoveZone(OdWorldClockViewModel clock)
    {
        if (!clock.IsLocal && Clocks.Remove(clock))
        {
            Save();
        }
    }

    partial void OnUse24HourChanged(bool value)
    {
        Save();
        Tick();
    }

    private void Save() => _save(new OdClockSettings
    {
        Zones = [.. Clocks.Where(clock => !clock.IsLocal).Select(clock => clock.Zone.Id)],
        Use24Hour = Use24Hour,
    });
}
