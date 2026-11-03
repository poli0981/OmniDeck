using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Stocks;

/// <summary>A range the detail view offers, with its name in the UI language.</summary>
public sealed class OdStockRangeOption(OdStockRange range, Func<string> label) : OdLocalizedOption(label)
{
    public OdStockRange Range { get; } = range;
}
