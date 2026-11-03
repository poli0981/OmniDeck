namespace OmniDeck.Modules.Stocks;

/// <summary>A listing on the watchlist, as the settings document keeps it.</summary>
public sealed record OdWatchItem(string Symbol, string? Mic, string Name, string? Currency)
{
    public OdTicker Ticker => new(Symbol, Mic);
}
