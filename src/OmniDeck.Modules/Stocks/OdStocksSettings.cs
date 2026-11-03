namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The Stocks module's settings document: the watchlist, in the user's order, and what the key's plan
/// allows, read from Twelve Data when the key was tested. The key itself is in the secret store.
/// </summary>
public sealed record OdStocksSettings
{
    public IReadOnlyList<OdWatchItem> Watchlist { get; init; } = [];

    /// <summary>The key's plan category, such as "basic"; null before a key was tested.</summary>
    public string? Plan { get; init; }

    public int MinuteLimit { get; init; } = OdCreditLimiter.FreeMinuteLimit;

    public int DayLimit { get; init; } = OdCreditLimiter.FreeDayLimit;

    /// <summary>How often quotes refresh while the market trades and the module is on screen.</summary>
    public int RefreshMinutes { get; init; } = 15;
}
