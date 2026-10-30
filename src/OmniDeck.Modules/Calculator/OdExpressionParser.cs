using System.Globalization;

namespace OmniDeck.Modules.Calculator;

/// <summary>A recursive-descent parser for <see cref="OdExpression"/>; one instance reads one expression.</summary>
internal sealed class OdExpressionParser(string text, bool degrees)
{
    private const int MaxFactorial = 170;

    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "sin", "cos", "tan", "asin", "acos", "atan", "sqrt", "ln", "log", "abs", "exp",
    };

    private int _position;

    private char Next => _position < text.Length ? text[_position] : '\0';

    public double Parse()
    {
        var value = ParseSum();
        SkipSpaces();
        if (_position < text.Length)
        {
            throw Syntax();
        }

        return value;
    }

    private static OdCalcException Domain() => new(OdCalcError.Domain, "Outside the function's domain");

    private static double Checked(double value)
    {
        if (double.IsNaN(value))
        {
            throw Domain();
        }

        return double.IsInfinity(value) ? throw new OdCalcException(OdCalcError.Overflow, "Result too large") : value;
    }

    private static double Factorial(double value)
    {
        if (value < 0 || !double.IsInteger(value))
        {
            throw Domain();
        }

        if (value > MaxFactorial)
        {
            throw new OdCalcException(OdCalcError.Overflow, "Result too large");
        }

        var result = 1d;
        for (var i = 2; i <= (int)value; i++)
        {
            result *= i;
        }

        return result;
    }

    private OdCalcException Syntax() => new(OdCalcError.Syntax, $"Unexpected input at position {_position}");

    private double ParseSum()
    {
        var value = ParseProduct();
        while (true)
        {
            SkipSpaces();
            if (Accept('+'))
            {
                value = Checked(value + ParseProduct());
            }
            else if (Accept('-') || Accept('−'))
            {
                value = Checked(value - ParseProduct());
            }
            else
            {
                return value;
            }
        }
    }

    private double ParseProduct()
    {
        var value = ParseSigned();
        while (true)
        {
            SkipSpaces();
            if (Accept('*') || Accept('×'))
            {
                value = Checked(value * ParseSigned());
            }
            else if (Accept('/') || Accept('÷'))
            {
                var divisor = ParseSigned();
                value = divisor is 0d
                    ? throw new OdCalcException(OdCalcError.DivideByZero, "Division by zero")
                    : Checked(value / divisor);
            }
            else if (StartsImplicitFactor())
            {
                value = Checked(value * ParseSigned());
            }
            else
            {
                return value;
            }
        }
    }

    // A leading sign binds looser than ^: −2^2 is −4, and 2^−1 is 0.5.
    private double ParseSigned()
    {
        SkipSpaces();
        if (Accept('-') || Accept('−'))
        {
            return -ParseSigned();
        }

        return Accept('+') ? ParseSigned() : ParsePower();
    }

    private double ParsePower()
    {
        var value = ParsePostfix();
        SkipSpaces();
        return Accept('^') ? Checked(Math.Pow(value, ParseSigned())) : value;
    }

    private double ParsePostfix()
    {
        var value = ParsePrimary();
        while (true)
        {
            SkipSpaces();
            if (Accept('!'))
            {
                value = Factorial(value);
            }
            else if (Accept('%'))
            {
                value /= 100;
            }
            else
            {
                return value;
            }
        }
    }

    private double ParsePrimary()
    {
        SkipSpaces();
        if (Accept('('))
        {
            var value = ParseSum();
            SkipSpaces();
            Expect(')');
            return value;
        }

        if (Accept('√'))
        {
            return SquareRoot(ParsePostfix());
        }

        if (Accept('π'))
        {
            return Math.PI;
        }

        if (char.IsAsciiDigit(Next) || Next == '.')
        {
            return ParseNumber();
        }

        return char.IsAsciiLetter(Next) ? ParseNamed() : throw Syntax();
    }

    private double ParseNumber()
    {
        var start = _position;
        while (char.IsAsciiDigit(Next) || Next == '.')
        {
            _position++;
        }

        return double.TryParse(text.AsSpan(start, _position - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Syntax();
    }

    private double ParseNamed()
    {
        var start = _position;
        while (char.IsAsciiLetter(Next))
        {
            _position++;
        }

        var name = text[start.._position].ToLowerInvariant();
        switch (name)
        {
            case "pi":
                return Math.PI;
            case "e":
                return Math.E;
            default:
                if (!Functions.Contains(name))
                {
                    throw Syntax();
                }

                break;
        }

        SkipSpaces();
        Expect('(');
        var argument = ParseSum();
        SkipSpaces();
        Expect(')');
        return Checked(Apply(name, argument));
    }

    private double Apply(string function, double x) => function switch
    {
        "sin" => degrees ? SinDegrees(x) : Math.Sin(x),
        "cos" => degrees ? SinDegrees(x + 90) : Math.Cos(x),
        "tan" => degrees ? TanDegrees(x) : Math.Tan(x),
        "asin" => Angle(Math.Asin(x)),
        "acos" => Angle(Math.Acos(x)),
        "atan" => Angle(Math.Atan(x)),
        "sqrt" => SquareRoot(x),
        "ln" => x > 0 ? Math.Log(x) : throw Domain(),
        "log" => x > 0 ? Math.Log10(x) : throw Domain(),
        "abs" => Math.Abs(x),
        _ => Math.Exp(x),
    };

    private double Angle(double radians) => degrees ? radians * 180 / Math.PI : radians;

    // Whole multiples of 90° come out exact, so sin 180° is 0 rather than 1.2e-16.
    private static double SinDegrees(double x)
    {
        var angle = ((x % 360) + 360) % 360;
        return angle switch
        {
            0d or 180d => 0,
            90d => 1,
            270d => -1,
            _ => Math.Sin(angle * Math.PI / 180),
        };
    }

    private static double TanDegrees(double x)
    {
        var angle = ((x % 180) + 180) % 180;
        return angle switch
        {
            0d => 0,
            90d => throw Domain(),
            _ => Math.Tan(angle * Math.PI / 180),
        };
    }

    private static double SquareRoot(double x) => x >= 0 ? Math.Sqrt(x) : throw Domain();

    private bool StartsImplicitFactor()
    {
        SkipSpaces();
        return Next is '(' or 'π' or '√' || char.IsAsciiLetter(Next);
    }

    private void SkipSpaces()
    {
        while (char.IsWhiteSpace(Next))
        {
            _position++;
        }
    }

    private bool Accept(char expected)
    {
        if (Next != expected)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void Expect(char expected)
    {
        if (!Accept(expected))
        {
            throw Syntax();
        }
    }
}
