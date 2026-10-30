using System.Windows.Data;
using System.Windows.Markup;

namespace OmniDeck.Core.Localization;

/// <summary>
/// <c>{res:OdLoc Key}</c>: a one-way binding to <see cref="OdLocalization"/>'s indexer, so the text
/// follows the UI language live.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class OdLocExtension : MarkupExtension
{
    public OdLocExtension()
    {
    }

    public OdLocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = OdLocalization.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
