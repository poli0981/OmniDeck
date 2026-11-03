namespace OmniDeck.Modules.Stocks;

/// <summary>
/// What a refresh got through: the request error that stopped it, if any; the listings that failed on
/// their own; when the minute's credits allow the rest; and whether today's credits ran out.
/// </summary>
public sealed record OdRefreshResult(OdStocksError? Error, IReadOnlyDictionary<string, OdStocksError> Failed, DateTimeOffset? NextAttempt, bool DayExhausted);
