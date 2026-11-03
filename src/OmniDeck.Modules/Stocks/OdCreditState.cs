namespace OmniDeck.Modules.Stocks;

/// <summary>
/// What <see cref="OdCreditLimiter"/> keeps across restarts in <c>app_meta</c>: the UTC day and the
/// credits spent on it, the spends of the last minute, and when a 429 lets requests go again.
/// </summary>
public sealed record OdCreditState(string Day, int Used, IReadOnlyList<OdCreditSpend> Recent, DateTimeOffset? BlockedUntil);
