namespace OmniDeck.Modules.Stocks;

/// <summary>A range the detail view offers, with its name in the UI language.</summary>
public sealed record OdStockRangeOption(OdStockRange Range, string Label)
{
    public override string ToString() => Label;
}
