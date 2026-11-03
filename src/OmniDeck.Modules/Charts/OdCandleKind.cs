namespace OmniDeck.Modules.Charts;

/// <summary>How a price chart draws each bar.</summary>
public enum OdCandleKind
{
    /// <summary>Candles: a body from open to close, hollow when the price rose, with wicks to the high and low.</summary>
    Candles,

    /// <summary>OHLC bars: a line from high to low, with the open ticked on the left and the close on the right.</summary>
    Bars,
}
