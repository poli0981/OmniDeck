namespace OmniDeck.Core.Governor;

/// <summary>A governor pass finished: what OmniDeck uses now, against which limits.</summary>
public sealed class OdGovernorSampledEventArgs(OdMemorySnapshot snapshot, OdGovernorOptions options) : EventArgs
{
    /// <summary>What the pass measured, less what it hibernated.</summary>
    public OdMemorySnapshot Snapshot { get; } = snapshot;

    public long CommittedBytes => Snapshot.TotalBytes;

    public OdGovernorOptions Options { get; } = options;
}
