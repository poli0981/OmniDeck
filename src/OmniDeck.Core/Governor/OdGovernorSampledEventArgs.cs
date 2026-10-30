namespace OmniDeck.Core.Governor;

/// <summary>A governor pass finished: what OmniDeck uses now, against which limits.</summary>
public sealed class OdGovernorSampledEventArgs(long committedBytes, OdGovernorOptions options) : EventArgs
{
    public long CommittedBytes { get; } = committedBytes;

    public OdGovernorOptions Options { get; } = options;
}
