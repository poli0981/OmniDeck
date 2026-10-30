using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MediaLab;

/// <summary>Monitor bounds for fullscreen, and screen captures that show what the user really sees.</summary>
internal static class Native
{
    /// <summary>The bounds, in device pixels, of the monitor the window is mostly on.</summary>
    public static Int32Rect MonitorBounds(Window window)
    {
        var monitor = MonitorFromWindow(new WindowInteropHelper(window).Handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(monitor, ref info);
        return new Int32Rect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top);
    }

    /// <summary>The window's on-screen rectangle in device pixels (DWM extended frame bounds).</summary>
    public static Int32Rect WindowBounds(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var r, Marshal.SizeOf<Rect>()) != 0)
        {
            GetWindowRect(hwnd, out r);
        }

        return new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    /// <summary>
    /// Copies a screen rectangle (composited by DWM, layered windows included) to a PNG. The video HWND
    /// and LibVLCSharp's overlay window are separate windows, so only a screen capture shows airspace.
    /// </summary>
    public static void CaptureScreen(Int32Rect area, string pngPath)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, area.Width, area.Height);
        var old = SelectObject(memDc, bitmap);
        try
        {
            BitBlt(memDc, 0, 0, area.Width, area.Height, screenDc, area.X, area.Y, SrcCopy | CaptureBlt);
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var file = File.Create(pngPath);
            encoder.Save(file);
        }
        finally
        {
            SelectObject(memDc, old);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private const uint MonitorDefaultToNearest = 2;
    private const int DwmwaExtendedFrameBounds = 9;
    private const uint SrcCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
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
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);
}
