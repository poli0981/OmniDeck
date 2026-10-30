using System.Globalization;
using System.Windows.Data;
using System.Windows.Documents;
using OmniDeck.App.Shell;

namespace OmniDeck.App.Legal;

/// <summary>Binds Markdown text to a <c>FlowDocumentScrollViewer</c>; links open in the default browser.</summary>
[ValueConversion(typeof(string), typeof(FlowDocument))]
public sealed class OdMarkdownConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        OdMarkdownRenderer.Render(value as string ?? "", OdExternal.Open);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
