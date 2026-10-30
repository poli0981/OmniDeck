using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Clock;

/// <summary>One clock: local time, or a time zone the user added.</summary>
public sealed partial class OdWorldClockViewModel(TimeZoneInfo zone, bool isLocal) : ObservableObject
{
    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private string _dateText = "";

    [ObservableProperty]
    private string _offsetText = "";

    public TimeZoneInfo Zone { get; } = zone;

    public bool IsLocal { get; } = isLocal;

    /// <summary>Every clock but local time can be removed.</summary>
    public bool CanRemove => !IsLocal;

    public string Title => IsLocal ? OdLocalization.Get("Clock_Local") : Zone.DisplayName;

    public void Update(DateTimeOffset utcNow, TimeZoneInfo local, bool use24Hour)
    {
        ArgumentNullException.ThrowIfNull(local);
        var culture = CultureInfo.CurrentCulture;
        var here = TimeZoneInfo.ConvertTime(utcNow, Zone);
        TimeText = here.ToString(use24Hour ? "HH:mm:ss" : "h:mm:ss tt", culture);
        DateText = here.ToString("D", culture);
        OffsetText = IsLocal ? "" : Offset(Zone.GetUtcOffset(utcNow) - local.GetUtcOffset(utcNow));
        OnPropertyChanged(nameof(Title));
    }

    public override string ToString() => Title;

    /// <summary>"3 h ahead", "5 h 30 min behind" or "Same time", relative to local time.</summary>
    private static string Offset(TimeSpan difference)
    {
        if (difference == TimeSpan.Zero)
        {
            return OdLocalization.Get("Clock_SameTime");
        }

        var size = difference.Duration();
        var amount = size.Minutes == 0
            ? OdLocalization.Format("Clock_Hours", (int)size.TotalHours)
            : OdLocalization.Format("Clock_HoursMinutes", (int)size.TotalHours, size.Minutes);
        return difference > TimeSpan.Zero ? OdLocalization.Format("Clock_Ahead", amount) : OdLocalization.Format("Clock_Behind", amount);
    }
}
