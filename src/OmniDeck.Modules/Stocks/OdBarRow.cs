namespace OmniDeck.Modules.Stocks;

/// <summary>One bar as a table row, the text alternative to the chart; <see cref="Text"/> is the whole row in words.</summary>
public sealed record OdBarRow(string Date, string Open, string High, string Low, string Close, string Volume, string Text)
{
    public override string ToString() => Text;
}
