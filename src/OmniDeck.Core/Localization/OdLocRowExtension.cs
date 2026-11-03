using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace OmniDeck.Core.Localization;

/// <summary>
/// <c>{res:OdLocRow Key, Path=Title}</c>: a control's label with the name of its row, such as
/// "Remove, Tokyo", for lists whose rows all have the same buttons. Screen readers then tell the rows'
/// buttons apart; the label comes first, as on screen. Both follow the UI language and the row live.
/// A label the row itself provides, such as Start or Pause, is <c>{res:OdLocRow LabelPath=…, Path=…}</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class OdLocRowExtension : MarkupExtension
{
    private static readonly IMultiValueConverter Joiner = new RowNameConverter();

    public OdLocRowExtension()
    {
    }

    public OdLocRowExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    /// <summary>The path to the row's name, from the row's data context.</summary>
    public string Path { get; set; } = "";

    /// <summary>The path to the label, when the row provides it instead of a key.</summary>
    public string LabelPath { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding { Converter = Joiner, Mode = BindingMode.OneWay };
        binding.Bindings.Add(Localized("A11y_RowAction"));
        binding.Bindings.Add(LabelPath.Length > 0 ? new Binding(LabelPath) { Mode = BindingMode.OneWay } : Localized(Key));
        binding.Bindings.Add(new Binding(Path) { Mode = BindingMode.OneWay });
        return binding.ProvideValue(serviceProvider);
    }

    private static Binding Localized(string key) => new($"[{key}]") { Source = OdLocalization.Instance, Mode = BindingMode.OneWay };

    private sealed class RowNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values switch
        {
            [string format, string label, var row] when row is not null && !string.IsNullOrEmpty(row.ToString()) =>
                string.Format(CultureInfo.CurrentCulture, format, label, row),
            [_, string label, ..] => label,
            _ => DependencyProperty.UnsetValue,
        };

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
