using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>The alarms part of the Timer screen: the list over <see cref="OdAlarmService"/> and the form that adds one.</summary>
public sealed partial class OdAlarmsViewModel : ObservableObject, IDisposable
{
    private readonly OdAlarmService _service;

    public OdAlarmsViewModel(OdAlarmService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        RepeatOptions =
        [
            new(OdAlarmRepeat.Once, () => OdLocalization.Get("Timer_RepeatOnce")),
            new(OdAlarmRepeat.Daily, () => OdLocalization.Get("Timer_RepeatDaily")),
            new(OdAlarmRepeat.Weekdays, () => OdLocalization.Get("Timer_RepeatWeekdays")),
            new(OdAlarmRepeat.Weekends, () => OdLocalization.Get("Timer_RepeatWeekends")),
        ];
        NewRepeat = RepeatOptions[1];
        service.Changed += OnChanged;
        Sync();
        OdLocalization.FollowCulture(OnCultureChanged);
    }

    public ObservableCollection<OdAlarmRowViewModel> Rows { get; } = [];

    public IReadOnlyList<OdAlarmRepeatOption> RepeatOptions { get; }

    /// <summary>How to type a time, in the culture's own form.</summary>
    public string TimeHint { get; private set; } = Hint();

    public bool HasNoAlarms => Rows.Count == 0;

    [ObservableProperty]
    private string _newTime = "";

    [ObservableProperty]
    private OdAlarmRepeatOption? _newRepeat;

    [ObservableProperty]
    private string _newLabel = "";

    /// <summary>Why the form could not add the alarm; null when it could.</summary>
    [ObservableProperty]
    private string? _message;

    public void Dispose()
    {
        OdLocalization.UnfollowCulture(OnCultureChanged);
        _service.Changed -= OnChanged;
    }

    [RelayCommand]
    private void Add()
    {
        if (!TimeOnly.TryParse(NewTime.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var time)
            && !TimeOnly.TryParse(NewTime.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            Message = OdLocalization.Format("Timer_AlarmBadTime", TimeHint);
            return;
        }

        _service.Add(time, NewRepeat?.Repeat ?? OdAlarmRepeat.Daily, NewLabel);
        NewTime = "";
        NewLabel = "";
        Message = null;
    }

    [RelayCommand]
    private void Remove(OdAlarmRowViewModel? row)
    {
        if (row is not null)
        {
            _service.Remove(row.Id);
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Sync();

    // A change of language: the repeats in place, so the form's choice stays, and the rows again.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var option in RepeatOptions)
        {
            option.RefreshLabel();
        }

        TimeHint = Hint();
        OnPropertyChanged(nameof(TimeHint));
        Sync();
    }

    private static string Hint() => OdLocalization.Format("Timer_AlarmTimeHint", new TimeOnly(7, 30).ToString("t", CultureInfo.CurrentCulture));

    private void Sync()
    {
        Rows.Clear();
        foreach (var alarm in _service.Alarms.OrderBy(alarm => alarm.Time))
        {
            var repeat = RepeatOptions.First(option => option.Repeat == alarm.Repeat).Label;
            Rows.Add(new OdAlarmRowViewModel(alarm, _service, repeat));
        }

        OnPropertyChanged(nameof(HasNoAlarms));
    }
}
