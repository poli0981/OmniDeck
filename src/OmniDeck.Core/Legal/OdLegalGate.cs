using System.Globalization;
using OmniDeck.Core.Storage;

namespace OmniDeck.Core.Legal;

/// <summary>
/// Whether the user has read the shipped legal documents (docs/SPEC.md §2): the acknowledged version
/// and its time live in <c>app_meta</c>, and a new <see cref="OdLegal.Version"/> asks again. This is
/// an acknowledgment, not a condition of the GPL.
/// </summary>
public sealed class OdLegalGate(IOdAppMeta meta, TimeProvider time)
{
    public bool IsAcknowledged =>
        string.Equals(meta.Get(OdAppMetaKeys.LegalAcknowledgedVersion), OdLegal.Version, StringComparison.Ordinal);

    /// <summary>When the user acknowledged the documents, in UTC; <see langword="null"/> before they did.</summary>
    public DateTimeOffset? AcknowledgedAt =>
        DateTimeOffset.TryParse(meta.Get(OdAppMetaKeys.LegalAcknowledgedAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    public void Acknowledge()
    {
        meta.Set(OdAppMetaKeys.LegalAcknowledgedVersion, OdLegal.Version);
        meta.Set(OdAppMetaKeys.LegalAcknowledgedAt, time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
    }
}
