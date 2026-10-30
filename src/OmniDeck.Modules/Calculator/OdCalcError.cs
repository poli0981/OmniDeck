namespace OmniDeck.Modules.Calculator;

/// <summary>Why an expression has no value.</summary>
public enum OdCalcError
{
    /// <summary>The input is not a well-formed expression.</summary>
    Syntax,

    /// <summary>A division or modulo by zero.</summary>
    DivideByZero,

    /// <summary>A function outside its domain, such as the square root of a negative number.</summary>
    Domain,

    /// <summary>A result too large to represent.</summary>
    Overflow,
}
