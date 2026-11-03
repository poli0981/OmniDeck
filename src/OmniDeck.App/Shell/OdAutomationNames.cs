using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// Names the buttons inside WPF-UI's templates that screen readers reach with no name, or with a
/// glyph's name such as "CaretUp24": the title bar's buttons, the sidebar's pane button, the scroll bar,
/// slider and number box buttons, a text box's clear button and the password box's reveal button. An
/// editable combo box's text box, which takes the focus, gets its combo box's name. Registered once,
/// for every window, on the first layout of each such button: WPF raises Loaded only on elements that
/// handle it themselves.
/// </summary>
internal static class OdAutomationNames
{
    private static readonly IMultiValueConverter Choose = new ChooseConverter();

    // By automation ID, which WPF-UI's templates set (or x:Name, which stands in for it).
    private static readonly Dictionary<string, Binding> ByPart = new(StringComparer.Ordinal)
    {
        ["TitleBarMinimizeButton"] = Localized("A11y_Minimize"),
        ["TitleBarCloseButton"] = Localized("A11y_Close"),
        ["PART_InlineIncrementButton"] = Localized("A11y_Increase"),
        ["PART_InlineDecrementButton"] = Localized("A11y_Decrease"),
        ["RevealButton"] = Localized("A11y_ShowPassword"),
        ["PART_ClearButton"] = Localized("A11y_Clear"),
        ["PageUp"] = Localized("A11y_PageUp"),
        ["PageDown"] = Localized("A11y_PageDown"),
        ["PageLeft"] = Localized("A11y_PageLeft"),
        ["PageRight"] = Localized("A11y_PageRight"),
        ["DecreaseLarge"] = Localized("A11y_Decrease"),
        ["IncreaseLarge"] = Localized("A11y_Increase"),
    };

    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(ButtonBase), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnButtonSized));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(OnTextBoxSized));
    }

    /// <summary>The name <paramref name="button"/> gets, as a binding to its string, or null when it is not one of these.</summary>
    internal static Binding? NameFor(ButtonBase button)
    {
        ArgumentNullException.ThrowIfNull(button);
        return (button.Command is { } command ? ByCommand(command) : null) ?? ByPart.GetValueOrDefault(PartOf(button));
    }

    /// <summary>The automation ID WPF-UI gave the button, or else its x:Name, which UI Automation reports in its place.</summary>
    private static string PartOf(ButtonBase button) =>
        AutomationProperties.GetAutomationId(button) is { Length: > 0 } id ? id : button.Name;

    private static Binding? ByCommand(ICommand command) => command switch
    {
        _ when command == ScrollBar.LineUpCommand => Localized("A11y_ScrollUp"),
        _ when command == ScrollBar.LineDownCommand => Localized("A11y_ScrollDown"),
        _ when command == ScrollBar.LineLeftCommand => Localized("A11y_ScrollLeft"),
        _ when command == ScrollBar.LineRightCommand => Localized("A11y_ScrollRight"),
        _ when command == ScrollBar.PageUpCommand => Localized("A11y_PageUp"),
        _ when command == ScrollBar.PageDownCommand => Localized("A11y_PageDown"),
        _ when command == ScrollBar.PageLeftCommand => Localized("A11y_PageLeft"),
        _ when command == ScrollBar.PageRightCommand => Localized("A11y_PageRight"),
        _ when command == Slider.DecreaseLarge => Localized("A11y_Decrease"),
        _ when command == Slider.IncreaseLarge => Localized("A11y_Increase"),
        _ => null,
    };

    private static void OnButtonSized(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ButtonBase button || !string.IsNullOrEmpty(AutomationProperties.GetName(button)))
        {
            return;
        }

        var part = PartOf(button);
        if (string.Equals(part, "TitleBarMaximizeButton", StringComparison.Ordinal))
        {
            // Maximize, or Restore once the window is maximized, as Windows' own title bars say.
            NameAfter(button, new Binding(nameof(Window.WindowState)) { RelativeSource = Ancestor(typeof(Window)), Converter = IsMaximized.Instance }, Localized("A11y_Maximize"), Localized("A11y_Restore"));
        }
        else if (string.Equals(part, "NavigationToggleButton", StringComparison.Ordinal))
        {
            NameAfter(button, new Binding(nameof(NavigationView.IsPaneOpen)) { RelativeSource = Ancestor(typeof(NavigationView)) }, Localized("A11y_OpenNavigation"), Localized("A11y_CloseNavigation"));
        }
        else if (NameFor(button) is { } name)
        {
            button.SetBinding(AutomationProperties.NameProperty, name);
        }
    }

    private static void OnTextBoxSized(object sender, SizeChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox { TemplatedParent: ComboBox combo } box && string.IsNullOrEmpty(AutomationProperties.GetName(box)))
        {
            box.SetBinding(AutomationProperties.NameProperty, new Binding { Path = new PropertyPath(AutomationProperties.NameProperty), Source = combo, Mode = BindingMode.OneWay });
        }
    }

    /// <summary>Names <paramref name="button"/> <paramref name="whenFalse"/> or <paramref name="whenTrue"/>, following <paramref name="condition"/>.</summary>
    private static void NameAfter(ButtonBase button, Binding condition, Binding whenFalse, Binding whenTrue)
    {
        var name = new MultiBinding { Converter = Choose, Mode = BindingMode.OneWay };
        name.Bindings.Add(condition);
        name.Bindings.Add(whenFalse);
        name.Bindings.Add(whenTrue);
        button.SetBinding(AutomationProperties.NameProperty, name);
    }

    private static RelativeSource Ancestor(Type type) => new(RelativeSourceMode.FindAncestor, type, 1);

    private static Binding Localized(string key) => new($"[{key}]") { Source = OdLocalization.Instance, Mode = BindingMode.OneWay };

    private sealed class ChooseConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values switch
        {
            [true, string, string whenTrue] => whenTrue,
            [_, string whenFalse, string] => whenFalse,
            _ => DependencyProperty.UnsetValue,
        };

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class IsMaximized : IValueConverter
    {
        public static readonly IsMaximized Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is WindowState.Maximized;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
