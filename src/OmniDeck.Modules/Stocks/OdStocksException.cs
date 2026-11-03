namespace OmniDeck.Modules.Stocks;

/// <summary>A market data request that failed, and why (<see cref="Error"/>). It never carries the key or a symbol.</summary>
public sealed class OdStocksException : Exception
{
    public OdStocksException()
        : this(OdStocksError.BadResponse)
    {
    }

    public OdStocksException(string message)
        : base(message)
    {
        Error = OdStocksError.BadResponse;
    }

    public OdStocksException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = OdStocksError.BadResponse;
    }

    public OdStocksException(OdStocksError error, Exception? innerException = null)
        : base("Twelve Data request failed: " + error, innerException)
    {
        Error = error;
    }

    public OdStocksError Error { get; }
}
