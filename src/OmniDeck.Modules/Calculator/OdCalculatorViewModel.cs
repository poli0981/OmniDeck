using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Calculator;

/// <summary>
/// The calculator (docs/SPEC.md §3.2): keypad and keyboard input build an expression, Enter evaluates
/// it into the session's history, and the result can be copied. It is the module's durable state:
/// hibernation drops the view, not this.
/// </summary>
public sealed partial class OdCalculatorViewModel : ObservableObject
{
    private readonly Action<OdCalculatorSettings> _save;
    private readonly Action<string> _copy;
    private bool _showsResult;

    [ObservableProperty]
    private string _expression = "";

    [ObservableProperty]
    private string _result = "0";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isScientific;

    [ObservableProperty]
    private bool _useDegrees;

    [ObservableProperty]
    private bool _historyIsEmpty = true;

    public OdCalculatorViewModel(OdCalculatorSettings settings, Action<OdCalculatorSettings> save, Action<string> copy)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _isScientific = settings.Scientific;
        _useDegrees = settings.Degrees;
        _save = save;
        _copy = copy;
        History.CollectionChanged += (_, _) => HistoryIsEmpty = History.Count == 0;
        StandardKeys = NewStandardKeys();
        OdLocalization.FollowCulture(OnCultureChanged);
    }

    /// <summary>This session's calculations, newest first.</summary>
    public ObservableCollection<OdCalcHistoryEntry> History { get; } = [];

    /// <summary>The standard keypad, row by row (four keys per row).</summary>
    public IReadOnlyList<OdCalcKey> StandardKeys { get; private set; }

    /// <summary>The scientific keys (five per row); functions type their "(", which the user closes.</summary>
    public IReadOnlyList<OdCalcKey> ScientificKeys { get; private set; } = NewScientificKeys();

    // A change of language: the keys again, with their names in it.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        StandardKeys = NewStandardKeys();
        ScientificKeys = NewScientificKeys();
        OnPropertyChanged(nameof(StandardKeys));
        OnPropertyChanged(nameof(ScientificKeys));
    }

    private static OdCalcKey[] NewStandardKeys() =>
    [
        new("C", "", OdCalcKeyKind.Clear, OdLocalization.Get("Calc_Clear")),
        new("⌫", "", OdCalcKeyKind.Backspace, OdLocalization.Get("Calc_Backspace")),
        Named("%", OdLocalization.Get("Calc_Percent")), Named("÷", OdLocalization.Get("Calc_Divide")),
        new("7"), new("8"), new("9"), Named("×", OdLocalization.Get("Calc_Multiply")),
        new("4"), new("5"), new("6"), new("−", "-", OdCalcKeyKind.Input, OdLocalization.Get("Calc_Minus")),
        new("1"), new("2"), new("3"), Named("+", OdLocalization.Get("Calc_Plus")),
        new("±", "", OdCalcKeyKind.Negate, OdLocalization.Get("Calc_Negate")),
        new("0"), Named(".", OdLocalization.Get("Calc_Decimal")),
        new("=", "", OdCalcKeyKind.Evaluate, OdLocalization.Get("Calc_Equals")),
    ];

    private static OdCalcKey[] NewScientificKeys() =>
    [
        Named("sin(", OdLocalization.Get("Calc_Sine")), Named("cos(", OdLocalization.Get("Calc_Cosine")), Named("tan(", OdLocalization.Get("Calc_Tangent")),
        Named("ln(", OdLocalization.Get("Calc_NaturalLog")), Named("log(", OdLocalization.Get("Calc_Log")),
        Named("asin(", OdLocalization.Get("Calc_Arcsine")), Named("acos(", OdLocalization.Get("Calc_Arccosine")), Named("atan(", OdLocalization.Get("Calc_Arctangent")),
        Named("√(", OdLocalization.Get("Calc_SquareRoot")), Named("^", OdLocalization.Get("Calc_Power")),
        Named("(", OdLocalization.Get("Calc_OpenParenthesis")), Named(")", OdLocalization.Get("Calc_CloseParenthesis")),
        Named("π", OdLocalization.Get("Calc_Pi")), Named("e", OdLocalization.Get("Calc_Euler")), Named("!", OdLocalization.Get("Calc_Factorial")),
    ];

    /// <summary>An input key that types its own label, with the name screen readers say for its symbol.</summary>
    private static OdCalcKey Named(string label, string name) => new(label, label, OdCalcKeyKind.Input, name);

    /// <summary>Starts a new session: an empty display and no history.</summary>
    public void Reset()
    {
        Clear();
        History.Clear();
    }

    [RelayCommand]
    private void Press(OdCalcKey key)
    {
        switch (key.Kind)
        {
            case OdCalcKeyKind.Clear:
                Clear();
                break;
            case OdCalcKeyKind.Backspace:
                Backspace();
                break;
            case OdCalcKeyKind.Negate:
                Negate();
                break;
            case OdCalcKeyKind.Evaluate:
                Evaluate();
                break;
            default:
                Input(key.Token);
                break;
        }
    }

    /// <summary>Adds a keypad token: a digit, an operator, a constant or a function with its "(".</summary>
    [RelayCommand]
    private void Input(string token)
    {
        if (_showsResult)
        {
            // After a result, an operator continues from it; anything else starts over.
            Expression = ContinuesResult(token) ? Result : "";
            _showsResult = false;
        }

        Expression += token;
        HasError = false;
    }

    [RelayCommand]
    private void Backspace()
    {
        _showsResult = false;
        if (Expression.Length > 0)
        {
            Expression = Expression[..^1];
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Expression = "";
        Result = "0";
        HasError = false;
        _showsResult = false;
    }

    /// <summary>Wraps the expression (or the last result) in a negation, or unwraps it again.</summary>
    [RelayCommand]
    private void Negate()
    {
        var current = _showsResult || Expression.Length == 0 ? Result : Expression;
        _showsResult = false;
        Expression = current.StartsWith("-(", StringComparison.Ordinal) && current.EndsWith(')')
            ? current[2..^1]
            : $"-({current})";
    }

    [RelayCommand]
    private void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(Expression))
        {
            return;
        }

        try
        {
            Result = OdExpression.Format(OdExpression.Evaluate(Expression, UseDegrees));
            HasError = false;
            History.Insert(0, new OdCalcHistoryEntry(Expression, Result));
            _showsResult = true;
        }
        catch (OdCalcException ex)
        {
            Result = ex.Error == OdCalcError.DivideByZero ? OdLocalization.Get("Calc_DivideByZero") : OdLocalization.Get("Calc_Error");
            HasError = true;
        }
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!HasError)
        {
            _copy(Result);
        }
    }

    /// <summary>Brings a history entry back to the display.</summary>
    [RelayCommand]
    private void Recall(OdCalcHistoryEntry entry)
    {
        Expression = entry.Expression;
        Result = entry.Result;
        HasError = false;
        _showsResult = true;
    }

    [RelayCommand]
    private void ClearHistory() => History.Clear();

    /// <summary>Removes one calculation from the history.</summary>
    [RelayCommand]
    private void Forget(OdCalcHistoryEntry entry) => History.Remove(entry);

    partial void OnIsScientificChanged(bool value) => Save();

    partial void OnUseDegreesChanged(bool value) => Save();

    private static bool ContinuesResult(string token) => token.Length > 0 && token[0] is '+' or '-' or '×' or '÷' or '^' or '%' or '!' or ')';

    private void Save() => _save(new OdCalculatorSettings { Scientific = IsScientific, Degrees = UseDegrees });
}
