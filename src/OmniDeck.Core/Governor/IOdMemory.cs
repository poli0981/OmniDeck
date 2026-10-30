namespace OmniDeck.Core.Governor;

/// <summary>What the governor measures and how it gives memory back (docs/RAM-GOVERNOR.md §4).</summary>
public interface IOdMemory
{
    /// <summary>This machine's physical memory, the base of the automatic caps.</summary>
    long PhysicalBytes { get; }

    /// <summary>
    /// Private commit of the processes OmniDeck runs: the shell in P1; the WebView2 processes join it
    /// in P2. Unlike the working set, it does not drop when Windows merely trims pages.
    /// </summary>
    long CommittedBytes();

    /// <summary>After demotions: a full collection that also compacts the large-object heap.</summary>
    void Reclaim();
}
