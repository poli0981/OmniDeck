using OmniDeck.Modules.Calculator;

namespace OmniDeck.Tests.ModuleApps;

public sealed class OdCalculatorViewModelTests
{
    private readonly List<OdCalculatorSettings> _saved = [];
    private readonly List<string> _copied = [];

    [Fact]
    public void Evaluate_ShowsTheResult_AndRecordsHistory()
    {
        var calculator = NewCalculator();

        Type(calculator, "1", "2", "+", "3", "0");
        calculator.EvaluateCommand.Execute(null);

        Assert.Equal("42", calculator.Result);
        Assert.False(calculator.HasError);
        Assert.Equal(new OdCalcHistoryEntry("12+30", "42"), Assert.Single(calculator.History));
    }

    [Fact]
    public void AfterAResult_AnOperatorContinues_AndADigitStartsOver()
    {
        var calculator = NewCalculator();
        Type(calculator, "6", "×", "7");
        calculator.EvaluateCommand.Execute(null);

        Type(calculator, "-", "2");
        Assert.Equal("42-2", calculator.Expression);
        calculator.EvaluateCommand.Execute(null);

        Type(calculator, "5");
        Assert.Equal("5", calculator.Expression);
        Assert.Equal(new[] { "40", "42" }, calculator.History.Select(entry => entry.Result), StringComparer.Ordinal);
    }

    [Fact]
    public void Errors_AreShownInWords_AndCannotBeCopied()
    {
        var calculator = NewCalculator();
        Type(calculator, "1", "÷", "0");
        calculator.EvaluateCommand.Execute(null);

        Assert.True(calculator.HasError);
        Assert.Equal("Can't divide by zero", calculator.Result);
        calculator.CopyResultCommand.Execute(null);
        Assert.Empty(_copied);
        Assert.Empty(calculator.History);

        calculator.ClearCommand.Execute(null);
        Type(calculator, "+");
        calculator.EvaluateCommand.Execute(null);
        Assert.Equal("Can't calculate that", calculator.Result);

        // Typing again clears the error mark.
        Type(calculator, "1");
        Assert.False(calculator.HasError);
    }

    [Fact]
    public void Editing_BackspaceClearAndNegate()
    {
        var calculator = NewCalculator();
        Type(calculator, "1", "2", "3");

        calculator.BackspaceCommand.Execute(null);
        Assert.Equal("12", calculator.Expression);
        calculator.NegateCommand.Execute(null);
        Assert.Equal("-(12)", calculator.Expression);
        calculator.NegateCommand.Execute(null);
        Assert.Equal("12", calculator.Expression);

        calculator.ClearCommand.Execute(null);
        Assert.Equal("", calculator.Expression);
        Assert.Equal("0", calculator.Result);
        calculator.BackspaceCommand.Execute(null);
        calculator.EvaluateCommand.Execute(null);
        Assert.Empty(calculator.History);

        // Negating with nothing typed negates the shown result.
        calculator.NegateCommand.Execute(null);
        Assert.Equal("-(0)", calculator.Expression);
    }

    [Fact]
    public void Copy_Recall_AndClearHistory()
    {
        var calculator = NewCalculator();
        Type(calculator, "2", "^", "1", "0");
        calculator.EvaluateCommand.Execute(null);
        var entry = calculator.History[0];

        calculator.CopyResultCommand.Execute(null);
        calculator.ClearCommand.Execute(null);
        calculator.RecallCommand.Execute(entry);

        Assert.Equal(new[] { "1024" }, _copied, StringComparer.Ordinal);
        Assert.Equal("2^10", calculator.Expression);
        Assert.Equal("1024", calculator.Result);

        calculator.ClearHistoryCommand.Execute(null);
        Assert.Empty(calculator.History);

        Type(calculator, "1");
        calculator.EvaluateCommand.Execute(null);
        calculator.Reset();
        Assert.Empty(calculator.History);
        Assert.Equal("", calculator.Expression);
    }

    [Fact]
    public void Modes_AreSaved()
    {
        var calculator = NewCalculator(new OdCalculatorSettings { Scientific = true, Degrees = false });
        Assert.True(calculator.IsScientific);
        Assert.False(calculator.UseDegrees);

        Type(calculator, "sin(", "π", "/", "2", ")");
        calculator.EvaluateCommand.Execute(null);
        Assert.Equal("1", calculator.Result);

        calculator.UseDegrees = true;
        calculator.IsScientific = false;

        Assert.Equal(new OdCalculatorSettings { Scientific = false, Degrees = true }, _saved[^1]);
        Assert.Equal(2, _saved.Count);
    }

    [Fact]
    public void Keypad_DispatchesEveryKind()
    {
        var calculator = NewCalculator();
        OdCalcKey Key(string label) => calculator.StandardKeys.Single(key => string.Equals(key.Label, label, StringComparison.Ordinal));

        foreach (var label in new[] { "7", "×", "6", "−", "1", "=" })
        {
            calculator.PressCommand.Execute(Key(label));
        }

        Assert.Equal("41", calculator.Result);
        calculator.PressCommand.Execute(Key("±"));
        Assert.Equal("-(41)", calculator.Expression);
        calculator.PressCommand.Execute(Key("⌫"));
        Assert.Equal("-(41", calculator.Expression);
        calculator.PressCommand.Execute(Key("C"));
        Assert.Equal("", calculator.Expression);

        Assert.Equal(20, calculator.StandardKeys.Count);
        Assert.Equal(15, calculator.ScientificKeys.Count);
        Assert.Equal("Clear", Key("C").AccessibleName);
        Assert.Equal("7", Key("7").AccessibleName);
        Assert.True(Key("=").IsPrimary);
        Assert.False(Key("7").IsPrimary);
        Assert.False(calculator.HistoryIsEmpty);
    }

    private static void Type(OdCalculatorViewModel calculator, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            calculator.InputCommand.Execute(token);
        }
    }

    private OdCalculatorViewModel NewCalculator(OdCalculatorSettings? settings = null) =>
        new(settings ?? new OdCalculatorSettings(), _saved.Add, _copied.Add);
}
