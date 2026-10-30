namespace OmniDeck.App.Legal;

/// <summary>One legal document as the gate shows it: its title and its Markdown text.</summary>
public sealed record OdLegalPage(string Title, string Markdown);
