using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OmniDeck.App.Shell;
using OmniDeck.Tests.TestSupport;
using NavigationView = Wpf.Ui.Controls.NavigationView;

namespace OmniDeck.Tests.Shell;

/// <summary>
/// The names <see cref="OdAutomationNames"/> gives template buttons, and an editable combo box's text box,
/// on their first layout once it is registered, as the shell does at startup.
/// </summary>
public sealed class OdAutomationNamesTests
{
    private static readonly Lazy<bool> Registered = new(() =>
    {
        OdAutomationNames.Register();
        return true;
    });

    [Fact]
    public void EveryScrollBarAndSliderCommand_NamesItsButton() => OdSta.Run(() =>
    {
        (ICommand Command, string Key)[] commands =
        [
            (ScrollBar.LineUpCommand, "A11y_ScrollUp"),
            (ScrollBar.LineDownCommand, "A11y_ScrollDown"),
            (ScrollBar.LineLeftCommand, "A11y_ScrollLeft"),
            (ScrollBar.LineRightCommand, "A11y_ScrollRight"),
            (ScrollBar.PageUpCommand, "A11y_PageUp"),
            (ScrollBar.PageDownCommand, "A11y_PageDown"),
            (ScrollBar.PageLeftCommand, "A11y_PageLeft"),
            (ScrollBar.PageRightCommand, "A11y_PageRight"),
            (Slider.DecreaseLarge, "A11y_Decrease"),
            (Slider.IncreaseLarge, "A11y_Increase"),
        ];

        foreach (var (command, key) in commands)
        {
            Assert.Equal($"[{key}]", NameOf(new RepeatButton { Command = command }));
        }

        // Any other command leaves the name to the part.
        Assert.Equal("[A11y_PageLeft]", NameOf(new RepeatButton { Command = ApplicationCommands.Copy, Name = "PageLeft" }));
        Assert.Null(NameOf(new RepeatButton { Command = ApplicationCommands.Copy }));
        return Task.CompletedTask;
    });

    [Fact]
    public void LaidOutButtons_TakeTheirNames_UnlessTheyHaveOne() => OdSta.Run(() =>
    {
        _ = Registered.Value;
        var pageUp = new RepeatButton { Name = "PageUp" };
        var reveal = new Button();
        AutomationProperties.SetAutomationId(reveal, "RevealButton");
        var named = new RepeatButton { Name = "PageDown" };
        AutomationProperties.SetName(named, "Mine");
        var other = new Button { Name = "Elsewhere" };

        LayOut(new StackPanel { Children = { pageUp, reveal, named, other } });

        Assert.Equal("Page up", AutomationProperties.GetName(pageUp));
        Assert.Equal("Show password", AutomationProperties.GetName(reveal));
        Assert.Equal("Mine", AutomationProperties.GetName(named));
        Assert.Empty(AutomationProperties.GetName(other));
        return Task.CompletedTask;
    });

    [Fact]
    public void TheMaximizeButton_SaysRestore_WhileTheWindowIsMaximized() => OdSta.Run(() =>
    {
        _ = Registered.Value;
        var maximize = new Button();
        AutomationProperties.SetAutomationId(maximize, "TitleBarMaximizeButton");
        LayOut(new StackPanel { Children = { maximize } });

        // The name follows the window's state through two converters; with no window here, they are called directly.
        var name = BindingOperations.GetMultiBinding(maximize, AutomationProperties.NameProperty);
        Assert.NotNull(name);
        var isMaximized = ((Binding)name.Bindings[0]).Converter;

        Assert.True(isMaximized.Convert(WindowState.Maximized, typeof(bool), parameter: null!, CultureInfo.InvariantCulture) is true);
        Assert.True(isMaximized.Convert(WindowState.Normal, typeof(bool), parameter: null!, CultureInfo.InvariantCulture) is false);
        Assert.Equal("Restore", Choose(name, true, "Maximize", "Restore"));
        Assert.Equal("Maximize", Choose(name, false, "Maximize", "Restore"));
        Assert.Same(DependencyProperty.UnsetValue, Choose(name, false, DependencyProperty.UnsetValue, "Restore"));
        Assert.Throws<NotSupportedException>(() => name.Converter.ConvertBack("Restore", [typeof(bool)], parameter: null!, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => isMaximized.ConvertBack(true, typeof(WindowState), parameter: null!, CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    });

    [Fact]
    public void TheSidebarsPaneButton_SaysWhetherItOpensOrClosesTheSidebar() => OdSta.Run(() =>
    {
        _ = Registered.Value;

        // Registers the pack:// scheme, which an Application otherwise does.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        var host = new Grid();
        host.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Wpf.Ui;component/Resources/Theme/Dark.xaml", UriKind.Relative)));
        host.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Wpf.Ui;component/Resources/Wpf.Ui.xaml", UriKind.Relative)));
        var view = new NavigationView { Style = (Style)host.FindResource(typeof(NavigationView)), IsPaneOpen = true };
        host.Children.Add(view);
        LayOut(host);

        var toggle = Descendants(view).OfType<ButtonBase>().Single(button => string.Equals(PartOf(button), "NavigationToggleButton", StringComparison.Ordinal));
        Assert.Equal("Close navigation", AutomationProperties.GetName(toggle));

        view.IsPaneOpen = false;
        Assert.Equal("Open navigation", AutomationProperties.GetName(toggle));
        return Task.CompletedTask;
    });

    [Fact]
    public void AnEditableComboBoxsTextBox_TakesTheComboBoxsName() => OdSta.Run(() =>
    {
        _ = Registered.Value;
        var combo = new ComboBox { IsEditable = true };
        AutomationProperties.SetName(combo, "Symbol");
        var alone = new TextBox();
        LayOut(new StackPanel { Children = { combo, alone } });

        var box = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
        Assert.Equal("Symbol", AutomationProperties.GetName(box));
        Assert.Empty(AutomationProperties.GetName(alone));

        AutomationProperties.SetName(combo, "Search");
        Assert.Equal("Search", AutomationProperties.GetName(box));
        return Task.CompletedTask;
    });

    private static string? NameOf(ButtonBase button) => OdAutomationNames.NameFor(button)?.Path.Path;

    private static string PartOf(ButtonBase button) =>
        AutomationProperties.GetAutomationId(button) is { Length: > 0 } id ? id : button.Name;

    private static object Choose(MultiBinding name, params object[] values) =>
        name.Converter.Convert(values, typeof(string), parameter: null!, CultureInfo.InvariantCulture);

    /// <summary>Measures, arranges and lays out <paramref name="root"/>, which raises each element's first SizeChanged.</summary>
    private static void LayOut(FrameworkElement root)
    {
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
