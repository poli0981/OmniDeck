using OmniDeck.Modules.Charts;

namespace OmniDeck.Modules.Stocks;

/// <summary>A listing's price bars over a range, oldest first, with the currency they are in.</summary>
public sealed record OdSeries(OdTicker Ticker, OdSeriesRange Range, string? Currency, IReadOnlyList<OdOhlc> Bars);
