using System.Globalization;

namespace OmniDeck.Modules.Timer;

/// <summary>Durations the way timers read: "4:59", "1:02:03", and "1:05.42" with hundredths for the stopwatch.</summary>
public static class OdDurationText
{
    /// <summary>Whole seconds; a countdown rounds up, so it shows 0:00 only once it has run out.</summary>
    public static string Format(TimeSpan duration, bool roundUp = false)
    {
        var seconds = (long)(roundUp ? Math.Ceiling(duration.TotalSeconds) : Math.Floor(duration.TotalSeconds));
        seconds = Math.Max(seconds, 0);
        return seconds >= 3600
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>Hundredths of a second, for the stopwatch.</summary>
    public static string FormatPrecise(TimeSpan duration)
    {
        var hundredths = Math.Max((long)Math.Floor(duration.TotalMilliseconds / 10), 0);
        return string.Create(CultureInfo.InvariantCulture, $"{Format(TimeSpan.FromSeconds(hundredths / 100))}.{hundredths % 100:00}");
    }
}
