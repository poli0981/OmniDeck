namespace OmniDeck.Modules.Stocks;

/// <summary>A listing: its symbol and, when known, the market it trades on, as an ISO 10383 MIC such as XNAS.</summary>
public sealed record OdTicker(string Symbol, string? Mic)
{
    /// <summary>
    /// The symbol as Twelve Data takes it: alone for a US listing, which the free plan covers, and
    /// otherwise with its market (SAP:XETR), so a paid plan's home-market listing is not mistaken for
    /// the US one.
    /// </summary>
    public string Query => Mic is null || OdMarketHours.IsUnitedStates(Mic) ? Symbol : Symbol + ":" + Mic;

    /// <summary>What the cache and the watchlist know the listing by.</summary>
    public string Key => Mic is null ? Symbol : Symbol + ":" + Mic;

    public override string ToString() => Key;
}
