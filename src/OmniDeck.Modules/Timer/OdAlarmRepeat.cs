namespace OmniDeck.Modules.Timer;

/// <summary>On which days an alarm rings.</summary>
public enum OdAlarmRepeat
{
    /// <summary>The next time the clock shows it, then the alarm turns itself off.</summary>
    Once,

    Daily,

    /// <summary>Monday to Friday.</summary>
    Weekdays,

    /// <summary>Saturday and Sunday.</summary>
    Weekends,
}
