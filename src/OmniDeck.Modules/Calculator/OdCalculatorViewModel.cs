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
        StandardKeys =
        [
            new("C", "", OdCalcKeyKind.Clear, OdLocalization.Get("Calc_Clear")),
            new("⌫", "", OdCalcKeyKind.Backspace, OdLocalization.Get("Calc_Backspace")),
            new("%"), new("÷"),
            new("7"), new("8"), new("9"), new("×"),
            new("4"), new("5"), new("6"), new("−", "-"),
            new("1"), new("2"), new("3"), new("+"),
            new("±", "", OdCalcKeyKind.Negate, OdLocalization.Get("Calc_Negate")),
            new("0"), new("."),
            new("=", "", OdCalcKeyKind.Evaluate),
        ];
    }

    /// <summary>This session's calculations, newest first.</summary>
    public ObservableCollection<OdCalcHistoryEntry> History { get; } = [];

    /// <summary>The standard keypad, row by row (four keys per row).</summary>
    public IReadOnlyList<OdCalcKey> StandardKeys { get; }

    /// <summary>The scientific keys (five per row); functions type their "(", which the user closes.</summary>
    public IReadOnlyList<OdCalcKey> ScientificKeys { get; } =
    [
        new("sin("), new("cos("), new("tan("), new("ln("), new("log("),
        new("asin("), new("acos("), new("atan("), new("√("), new("^"),
        new("("), new(")"), new("π"), new("e"), new("!"),
    ];

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

    partial void OnIsScientificChanged(bool value) => Save();

    partial void OnUseDegreesChanged(bool value) => Save();

    private static bool ContinuesResult(string token) => token.Length > 0 && token[0] is '+' or '-' or '×' or '÷' or '^' or '%' or '!' or ')';

    private void Save() => _save(new OdCalculatorSettings { Scientific = IsScientific, Degrees = UseDegrees });
}
