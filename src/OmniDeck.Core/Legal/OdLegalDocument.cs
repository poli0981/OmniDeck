namespace OmniDeck.Core.Legal;

/// <summary>One legal document: its file in the app's <c>legal</c> folder and the resource key of its title.</summary>
public sealed record OdLegalDocument(string FileName, string TitleKey);
