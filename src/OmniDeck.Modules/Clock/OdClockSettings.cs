namespace OmniDeck.Modules.Clock;

/// <summary>The world clock's zones and its 12/24-hour choice.</summary>
public sealed record OdClockSettings
{
    /// <summary>Windows time-zone ids, in the order the user added them; local time always comes first.</summary>
    public IReadOnlyList<string> Zones { get; init; } = [];

    /// <summary>Null follows the regional format.</summary>
    public bool? Use24Hour { get; init; }
}
