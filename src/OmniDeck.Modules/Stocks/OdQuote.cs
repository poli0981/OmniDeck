namespace OmniDeck.Modules.Stocks;

/// <summary>
/// A listing's latest quote. Every figure may be missing, as a currency pair has no volume, and the
/// screen shows N/A for what is.
/// </summary>
public sealed record OdQuote(
    OdTicker Ticker,
    string? Name,
    string? Exchange,
    string? Currency,
    decimal? Price,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? PreviousClose,
    decimal? Change,
    decimal? PercentChange,
    long? Volume,
    decimal? YearLow,
    decimal? YearHigh,
    bool? IsMarketOpen,
    DateTimeOffset? AsOf);
