namespace OmniDeck.Modules.Timer;

/// <summary>
/// An alarm: the time of day it rings, on which days, its label and whether it is on.
/// <paramref name="ArmedAt"/> is when it was set or last switched on, in local time: a one-off alarm
/// rings the first time its time comes after that.
/// </summary>
public sealed record OdAlarm(string Id, TimeOnly Time, OdAlarmRepeat Repeat, string Label, bool IsOn, DateTime ArmedAt);
