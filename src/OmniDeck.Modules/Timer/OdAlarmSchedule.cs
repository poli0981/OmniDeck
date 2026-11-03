namespace OmniDeck.Modules.Timer;

/// <summary>
/// When an alarm rings next, in local wall-clock time. A time that a daylight-saving change skips (02:30
/// on the night clocks go forward) rings as soon as the clock is past the gap; a time that happens twice
/// (on the night clocks go back) rings once, the first time.
/// </summary>
public static class OdAlarmSchedule
{
    // Alarms ring within a week, whatever their days.
    private const int DaysAhead = 8;

    /// <summary>The first time after <paramref name="after"/> that <paramref name="alarm"/> rings; null when it is off.</summary>
    public static DateTime? NextAt(OdAlarm alarm, DateTime after, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        ArgumentNullException.ThrowIfNull(zone);
        if (!alarm.IsOn)
        {
            return null;
        }

        var from = alarm.Repeat == OdAlarmRepeat.Once && alarm.ArmedAt > after ? alarm.ArmedAt : after;
        for (var day = 0; day <= DaysAhead; day++)
        {
            var date = from.Date.AddDays(day);
            if (!RingsOn(alarm.Repeat, date.DayOfWeek))
            {
                continue;
            }

            var at = WallClock(date.Add(alarm.Time.ToTimeSpan()), zone);
            if (at > from)
            {
                return at;
            }
        }

        return null;
    }

    private static bool RingsOn(OdAlarmRepeat repeat, DayOfWeek day) => repeat switch
    {
        OdAlarmRepeat.Weekdays => day is not (DayOfWeek.Saturday or DayOfWeek.Sunday),
        OdAlarmRepeat.Weekends => day is DayOfWeek.Saturday or DayOfWeek.Sunday,
        _ => true,
    };

    /// <summary>A local time that does exist: one inside a gap moves forward, past it.</summary>
    private static DateTime WallClock(DateTime local, TimeZoneInfo zone)
    {
        var moved = local;
        for (var i = 0; i < 4 && zone.IsInvalidTime(moved); i++)
        {
            moved = moved.AddMinutes(30);
        }

        return moved;
    }
}
