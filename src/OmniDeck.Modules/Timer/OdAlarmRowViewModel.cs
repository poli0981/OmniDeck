using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Timer;

/// <summary>An alarm as a row: its time, days and label, when it rings next, and its on/off switch.</summary>
public sealed partial class OdAlarmRowViewModel : ObservableObject
{
    private readonly OdAlarmService _service;
    private bool _showing;

    public OdAlarmRowViewModel(OdAlarm alarm, OdAlarmService service, string repeat)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        _service = service;
        Id = alarm.Id;
        Time = alarm.Time.ToString("t", CultureInfo.CurrentCulture);
        Repeat = repeat;
        Label = alarm.Label;
        Show(alarm);
    }

    public string Id { get; }

    public string Time { get; }

    public string Repeat { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private string _next = "";

    [ObservableProperty]
    private string _accessibleName = "";

    public void Show(OdAlarm alarm)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        _showing = true;
        IsOn = alarm.IsOn;
        _showing = false;
        Next = _service.NextAt(alarm) is { } at
            ? OdLocalization.Format("Timer_AlarmNext", at.ToString("dddd", CultureInfo.CurrentCulture), at.ToString("t", CultureInfo.CurrentCulture))
            : OdLocalization.Get("Timer_AlarmOff");
        var label = Label.Length > 0 ? Label : OdLocalization.Get("Timer_AlarmTitle");
        AccessibleName = OdLocalization.Format("Timer_AlarmRow", label, Time, Repeat, Next);
    }

    public override string ToString() => AccessibleName;

    partial void OnIsOnChanged(bool value)
    {
        if (!_showing)
        {
            _service.SetOn(Id, value);
        }
    }
}
