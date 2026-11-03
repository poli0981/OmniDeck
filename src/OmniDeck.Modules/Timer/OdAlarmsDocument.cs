namespace OmniDeck.Modules.Timer;

/// <summary>The alarms as their settings document keeps them.</summary>
public sealed record OdAlarmsDocument(IReadOnlyList<OdAlarm> Alarms);
