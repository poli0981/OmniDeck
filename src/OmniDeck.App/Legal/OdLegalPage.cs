namespace OmniDeck.App.Legal;

/// <summary>One legal document as the gate shows it: its title and its Markdown text.</summary>
public sealed record OdLegalPage(string Title, string Markdown)
{
    /// <summary>The title: screen readers name a tab after its data, which would otherwise be the whole text.</summary>
    public override string ToString() => Title;
}
