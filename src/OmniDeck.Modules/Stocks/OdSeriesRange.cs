namespace OmniDeck.Modules.Stocks;

/// <summary>
/// The price series OmniDeck asks for, one credit each: a year of daily bars, which also serves the
/// one-month and six-month views and the sparklines, and five years of weekly bars.
/// </summary>
public enum OdSeriesRange
{
    /// <summary>252 daily bars, about a year of trading days.</summary>
    Daily,

    /// <summary>261 weekly bars, about five years.</summary>
    Weekly,
}
