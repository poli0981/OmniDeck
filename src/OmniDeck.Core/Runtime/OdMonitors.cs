using System.Runtime.InteropServices;
using System.Windows;

namespace OmniDeck.Core.Runtime;

/// <summary>The monitors windows are on, for covering one in full screen.</summary>
public static partial class OdMonitors
{
    private const uint DefaultToNearest = 2;

    /// <summary>The bounds, in device pixels, of the monitor <paramref name="window"/> is mostly on; empty if Windows cannot say.</summary>
    public static Int32Rect BoundsOf(nint window)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(window, DefaultToNearest), ref info)
            ? new Int32Rect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top)
            : Int32Rect.Empty;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Bounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Bounds Monitor;
        public Bounds Work;
        public uint Flags;
    }
}
