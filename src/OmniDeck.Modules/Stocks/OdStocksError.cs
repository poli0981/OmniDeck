namespace OmniDeck.Modules.Stocks;

/// <summary>Why market data did not come, each with its own message on screen.</summary>
public enum OdStocksError
{
    /// <summary>No key has been entered yet.</summary>
    NoKey,

    /// <summary>Twelve Data does not accept the key.</summary>
    KeyRejected,

    /// <summary>The listing needs a higher Twelve Data plan than the key's: not a failure, a limit of the plan.</summary>
    PlanRequired,

    /// <summary>Twelve Data does not know the symbol.</summary>
    NotFound,

    /// <summary>This minute's credits are spent; they come back at the next minute.</summary>
    MinuteLimit,

    /// <summary>Today's credits are spent; they come back at midnight UTC.</summary>
    DayLimit,

    /// <summary>Twelve Data could not be reached, or did not answer in time.</summary>
    Offline,

    /// <summary>The answer could not be read.</summary>
    BadResponse,
}
