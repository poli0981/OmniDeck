namespace OmniDeck.Modules.Stocks;

/// <summary>
/// A search result: a listing and the Twelve Data plan it needs (Basic, the free plan, or Grow, Pro
/// and so on), so the screen can say which ones the user's key reaches before spending a credit.
/// </summary>
public sealed record OdListing(string Symbol, string Name, string? Exchange, string? Mic, string? Country, string? Currency, string? Type, string? Plan)
{
    public OdTicker Ticker => new(Symbol, Mic);
}
