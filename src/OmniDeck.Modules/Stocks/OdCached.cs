namespace OmniDeck.Modules.Stocks;

/// <summary>A cached value and when it was fetched.</summary>
public sealed record OdCached<T>(T Value, DateTimeOffset FetchedAt);
