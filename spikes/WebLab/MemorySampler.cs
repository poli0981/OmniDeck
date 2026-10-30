using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WebLab;

/// <summary>One process in one sample. All sizes in MiB.</summary>
public sealed record ProcessSample(
    int Pid, string Kind, string Attributed, double PrivateCommitMb, double PrivateWsMb, double WsMb, double SharedCommitMb, double CpuMs);

/// <summary>
/// The three candidate governor metrics side by side (docs/RAM-GOVERNOR.md §4): private commit
/// (Process.PrivateMemorySize64), private working set (PROCESS_MEMORY_COUNTERS_EX2) and working set.
/// </summary>
public static class MemorySampler
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint ProcessVmRead = 0x0010;
    private const double MiB = 1024 * 1024;

    public static ProcessSample? Sample(int pid, string kind, string attributed)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var privateCommit = process.PrivateMemorySize64 / MiB;
            var ws = process.WorkingSet64 / MiB;

            double privateWs = double.NaN, sharedCommit = double.NaN, cpuMs = double.NaN;
            using var handle = OpenProcess(ProcessQueryLimitedInformation | ProcessVmRead, false, pid) is { IsInvalid: false } h
                ? h
                : OpenProcess(ProcessQueryLimitedInformation, false, pid);
            if (!handle.IsInvalid)
            {
                var counters = new ProcessMemoryCountersEx2 { Cb = (uint)Marshal.SizeOf<ProcessMemoryCountersEx2>() };
                if (GetProcessMemoryInfo(handle, ref counters, counters.Cb))
                {
                    privateWs = counters.PrivateWorkingSetSize / MiB;
                    sharedCommit = counters.SharedCommitUsage / MiB;
                }

                if (GetProcessTimes(handle, out _, out _, out var kernel, out var user))
                {
                    cpuMs = (kernel + user) / 10_000.0;
                }
            }

            return new ProcessSample(pid, kind, attributed, privateCommit, privateWs, ws, sharedCommit, cpuMs);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null; // exited between enumeration and sampling
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx2
    {
        public uint Cb;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
        public nuint PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(SafeProcessHandle process, ref ProcessMemoryCountersEx2 counters, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
}
