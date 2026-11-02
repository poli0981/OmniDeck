namespace OmniDeck.Core.Governor;

/// <summary>What the governor measures and how it gives memory back (docs/RAM-GOVERNOR.md §4).</summary>
public interface IOdMemory
{
    /// <summary>This machine's physical memory, the base of the automatic caps.</summary>
    long PhysicalBytes { get; }

    /// <summary>
    /// Private commit of OmniDeck's own process, where the shell and the built-in modules run; the
    /// processes it starts besides come from <see cref="IOdMemoryReporter"/>. Unlike the working set,
    /// it does not drop when Windows merely trims pages.
    /// </summary>
    long CommittedBytes();

    /// <summary>After demotions: a full collection that also compacts the large-object heap.</summary>
    void Reclaim();
}
