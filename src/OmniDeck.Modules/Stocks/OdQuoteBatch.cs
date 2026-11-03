namespace OmniDeck.Modules.Stocks;

/// <summary>
/// What one batch of quotes brought, by <see cref="OdTicker.Key"/>: the quotes, and the listings that
/// failed on their own (a plan limit, an unknown symbol) while the rest came through.
/// </summary>
public sealed record OdQuoteBatch(IReadOnlyDictionary<string, OdQuote> Quotes, IReadOnlyDictionary<string, OdStocksError> Failed);
