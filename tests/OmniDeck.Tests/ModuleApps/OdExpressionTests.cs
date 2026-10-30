using OmniDeck.Modules.Calculator;

namespace OmniDeck.Tests.ModuleApps;

public sealed class OdExpressionTests
{
    [Theory]
    [InlineData("1+2*3", "7")]
    [InlineData("(1+2)*3", "9")]
    [InlineData("10-4-3", "3")]
    [InlineData("100÷4÷5", "5")]
    [InlineData("2×3", "6")]
    [InlineData("8/2/2", "2")]
    [InlineData("2^3^2", "512")]
    [InlineData("-2^2", "-4")]
    [InlineData("2^-1", "0.5")]
    [InlineData("−3+5", "2")]
    [InlineData("+4", "4")]
    [InlineData("--4", "4")]
    [InlineData("0.1+0.2", "0.3")]
    [InlineData(".5*4", "2")]
    [InlineData("50%", "0.5")]
    [InlineData("200*10%", "20")]
    [InlineData("5!", "120")]
    [InlineData("0!", "1")]
    [InlineData("3!!", "720")]
    [InlineData("1/3", "0.333333333333")]
    [InlineData(" 2 * ( 3 + 4 ) ", "14")]
    [InlineData("2(3+4)", "14")]
    [InlineData("2π", "6.28318530718")]
    [InlineData("pi", "3.14159265359")]
    [InlineData("e", "2.71828182846")]
    [InlineData("√(16)", "4")]
    [InlineData("√9", "3")]
    [InlineData("sqrt(2)", "1.41421356237")]
    [InlineData("abs(-7)", "7")]
    [InlineData("ln(e)", "1")]
    [InlineData("log(1000)", "3")]
    [InlineData("exp(0)", "1")]
    [InlineData("10^20", "1E+20")]
    [InlineData("3-3", "0")]
    [InlineData("-0", "0")]
    public void Evaluates(string expression, string expected)
    {
        Assert.Equal(expected, OdExpression.Format(OdExpression.Evaluate(expression, degrees: true)));
    }

    [Theory]
    [InlineData("sin(30)", "0.5")]
    [InlineData("sin(180)", "0")]
    [InlineData("sin(-90)", "-1")]
    [InlineData("sin(270)", "-1")]
    [InlineData("cos(60)", "0.5")]
    [InlineData("cos(90)", "0")]
    [InlineData("cos(360)", "1")]
    [InlineData("tan(45)", "1")]
    [InlineData("tan(180)", "0")]
    [InlineData("asin(1)", "90")]
    [InlineData("acos(0)", "90")]
    [InlineData("atan(1)", "45")]
    [InlineData("SIN(30)", "0.5")]
    public void Trigonometry_InDegrees_IsExactAtRightAngles(string expression, string expected)
    {
        Assert.Equal(expected, OdExpression.Format(OdExpression.Evaluate(expression, degrees: true)));
    }

    [Theory]
    [InlineData("sin(π/2)", "1")]
    [InlineData("cos(0)", "1")]
    [InlineData("tan(0)", "0")]
    [InlineData("atan(1)*4", "3.14159265359")]
    public void Trigonometry_InRadians(string expression, string expected)
    {
        Assert.Equal(expected, OdExpression.Format(OdExpression.Evaluate(expression, degrees: false)));
    }

    [Theory]
    [InlineData("", OdCalcError.Syntax)]
    [InlineData("1+", OdCalcError.Syntax)]
    [InlineData("(1+2", OdCalcError.Syntax)]
    [InlineData("1+2)", OdCalcError.Syntax)]
    [InlineData("1..2", OdCalcError.Syntax)]
    [InlineData(".", OdCalcError.Syntax)]
    [InlineData("2 3", OdCalcError.Syntax)]
    [InlineData("foo(1)", OdCalcError.Syntax)]
    [InlineData("sin 30", OdCalcError.Syntax)]
    [InlineData("#", OdCalcError.Syntax)]
    [InlineData("1/0", OdCalcError.DivideByZero)]
    [InlineData("5÷(2-2)", OdCalcError.DivideByZero)]
    [InlineData("√(-1)", OdCalcError.Domain)]
    [InlineData("ln(0)", OdCalcError.Domain)]
    [InlineData("log(-5)", OdCalcError.Domain)]
    [InlineData("asin(2)", OdCalcError.Domain)]
    [InlineData("tan(90)", OdCalcError.Domain)]
    [InlineData("(-1)^0.5", OdCalcError.Domain)]
    [InlineData("2.5!", OdCalcError.Domain)]
    [InlineData("(-3)!", OdCalcError.Domain)]
    [InlineData("171!", OdCalcError.Overflow)]
    [InlineData("10^400", OdCalcError.Overflow)]
    [InlineData("exp(1000)", OdCalcError.Overflow)]
    [InlineData("1e400", OdCalcError.Syntax)]
    public void Rejects(string expression, OdCalcError error)
    {
        var exception = Assert.Throws<OdCalcException>(() => OdExpression.Evaluate(expression, degrees: true));

        Assert.Equal(error, exception.Error);
    }

    [Fact]
    public void Exceptions_CarryAMessage()
    {
        Assert.Equal("boom", new OdCalcException("boom").Message);
        Assert.Equal(OdCalcError.Syntax, new OdCalcException().Error);
        Assert.IsType<InvalidOperationException>(new OdCalcException("outer", new InvalidOperationException()).InnerException);
    }
}
