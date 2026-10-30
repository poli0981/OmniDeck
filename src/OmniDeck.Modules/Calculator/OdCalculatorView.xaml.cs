using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OmniDeck.Modules.Calculator;

/// <summary>
/// The calculator's keypad, display and history. Typing works as on the keypad: digits and operators
/// (* and / for × and ÷), Enter or = to evaluate, Backspace, Esc to clear, Ctrl+C to copy the result.
/// </summary>
public partial class OdCalculatorView
{
    public OdCalculatorView()
    {
        InitializeComponent();
    }

    private OdCalculatorViewModel? ViewModel => DataContext as OdCalculatorViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e) => Focus();

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (ViewModel is not { } calculator || e.OriginalSource is TextBox)
        {
            return;
        }

        foreach (var c in e.Text)
        {
            if (c == '=')
            {
                calculator.EvaluateCommand.Execute(null);
                continue;
            }

            var token = c switch
            {
                '*' => "×",
                '/' => "÷",
                _ when char.IsAsciiDigit(c) || "+-^%!().".Contains(c, StringComparison.Ordinal) => c.ToString(),
                _ => null,
            };
            if (token is not null)
            {
                calculator.InputCommand.Execute(token);
            }
        }

        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is not { } calculator)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                calculator.EvaluateCommand.Execute(null);
                break;
            case Key.Back:
                calculator.BackspaceCommand.Execute(null);
                break;
            case Key.Escape:
                calculator.ClearCommand.Execute(null);
                break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                calculator.CopyResultCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: OdCalcHistoryEntry entry })
        {
            ViewModel?.RecallCommand.Execute(entry);
        }
    }
}
