using System.Globalization;

namespace OmniDeck.Modules.Calculator;

/// <summary>
/// The calculator's evaluator (docs/SPEC.md §3.2). It reads + − × ÷, ^ (right-associative, and
/// tighter than a leading minus: −2^2 is −4), postfix ! and %, parentheses, π and e, the functions
/// sin cos tan asin acos atan sqrt (√) ln log abs exp, and implicit multiplication before a
/// parenthesis, a constant or a function (2π, 3(4+1)). Numbers use "." as the decimal point
/// whatever the UI language, like the keypad. x% is x/100.
/// </summary>
public static class OdExpression
{
    /// <summary>The value of <paramref name="text"/>; trigonometry works in degrees when <paramref name="degrees"/> is set.</summary>
    /// <exception cref="OdCalcException">The text is not an expression, or it has no finite value.</exception>
    public static double Evaluate(string text, bool degrees)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new OdExpressionParser(text, degrees).Parse();
    }

    /// <summary>A result as the display shows it: up to 12 significant digits, so 0.1 + 0.2 reads 0.3.</summary>
    public static string Format(double value) =>
        value is 0d ? "0" : value.ToString("G12", CultureInfo.InvariantCulture);
}
