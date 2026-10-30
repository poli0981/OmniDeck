using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;

namespace OmniDeck.Core.Governor;

/// <summary><see cref="IOdMemory"/> for this process (P1: native modules run in the shell).</summary>
public sealed class OdProcessMemory : IOdMemory
{
    public long PhysicalBytes { get; } = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public long CommittedBytes()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64;
    }

    [SuppressMessage("Major Code Smell", "S1215:\"GC.Collect\" should not be called", Justification = "Demoted modules free memory only once it is collected (docs/RAM-GOVERNOR.md §4).")]
    public void Reclaim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
