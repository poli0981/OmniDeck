namespace OmniDeck.Core.Storage;

/// <summary>The keys of <c>app_meta</c>.</summary>
public static class OdAppMetaKeys
{
    public const string SchemaVersion = "schema_version";

    /// <summary>The legal-document version the user acknowledged (docs/SPEC.md §2).</summary>
    public const string LegalAcknowledgedVersion = "legal.acknowledged_version";

    /// <summary>When it was acknowledged, as an ISO 8601 UTC timestamp.</summary>
    public const string LegalAcknowledgedAt = "legal.acknowledged_at";
}
