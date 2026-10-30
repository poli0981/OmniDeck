namespace OmniDeck.Core.Legal;

/// <summary>The legal documents OmniDeck ships in its <c>legal</c> folder (docs/legal) and their version.</summary>
public static class OdLegal
{
    /// <summary>The version every document in docs/legal carries; a test keeps the two in step.</summary>
    public const string Version = "1.1";

    /// <summary>The documents the legal gate shows, in order.</summary>
    public static IReadOnlyList<OdLegalDocument> Documents { get; } =
    [
        new("EULA.md", "Legal_Eula"),
        new("DISCLAIMER.md", "Legal_Disclaimer"),
        new("PRIVACY.md", "Legal_Privacy"),
        new("THIRD-PARTY-NOTICES.md", "Legal_Notices"),
    ];
}
