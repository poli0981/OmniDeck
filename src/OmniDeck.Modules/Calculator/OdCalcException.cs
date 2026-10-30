namespace OmniDeck.Modules.Calculator;

/// <summary>An expression the calculator cannot evaluate, and why.</summary>
public sealed class OdCalcException : Exception
{
    public OdCalcException()
    {
    }

    public OdCalcException(string message)
        : base(message)
    {
    }

    public OdCalcException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public OdCalcException(OdCalcError error, string message)
        : base(message)
    {
        Error = error;
    }

    public OdCalcError Error { get; }
}
