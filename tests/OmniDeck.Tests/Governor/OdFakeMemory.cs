using OmniDeck.Core.Governor;

namespace OmniDeck.Tests.Governor;

/// <summary>A memory gauge the test sets, which drops by <see cref="FreedPerReclaim"/> on every reclaim.</summary>
public sealed class OdFakeMemory : IOdMemory
{
    public long PhysicalBytes { get; set; } = 32 * OdGovernorOptions.GiB;

    public long Committed { get; set; } = 500 * OdGovernorOptions.MiB;

    /// <summary>What each reclaim gives back, as a hibernated module would.</summary>
    public long FreedPerReclaim { get; set; }

    public int Reclaims { get; private set; }

    public long CommittedBytes() => Committed;

    public void Reclaim()
    {
        Reclaims++;
        Committed -= FreedPerReclaim;
    }
}
