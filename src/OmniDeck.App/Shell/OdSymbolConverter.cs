using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// Turns the icon names modules declare (Core stays free of WPF-UI) into Fluent symbols; an unknown
/// name shows a generic app glyph rather than nothing.
/// </summary>
[ValueConversion(typeof(string), typeof(SymbolRegular))]
public sealed class OdSymbolConverter : IValueConverter
{
    public static SymbolRegular Parse(string? name) =>
        Enum.TryParse<SymbolRegular>(name, ignoreCase: false, out var symbol) && Enum.IsDefined(symbol) ? symbol : SymbolRegular.Apps24;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Parse(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
