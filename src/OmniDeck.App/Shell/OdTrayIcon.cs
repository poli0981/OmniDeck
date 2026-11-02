using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon.Interop;

namespace OmniDeck.App.Shell;

/// <summary>
/// Draws the tray icon, a bold "O" on the accent color, and redraws it only when the colors change.
/// H.NotifyIcon's <c>GeneratedIconSource</c> is not used: in 2.4.1 every redraw leaks an icon handle
/// and every brush change adds one more redraw handler, so a few accent changes used up the process's
/// GDI objects ("A generic error occurred in GDI+"). These icons are read from ICO bytes, so each one
/// owns its handle, and the taskbar icon disposes the previous icon when the next one is set.
/// </summary>
internal sealed class OdTrayIcon
{
    // The first accent swatch in Settings → Appearance, for a theme that offers no accent brush.
    private static readonly Color FallbackAccent = Color.FromRgb(0x00, 0x67, 0xC0);

    private (Color Background, Color Foreground)? _drawn;

    /// <summary>
    /// A new icon in the theme's colors, or null when they match the icon drawn last. The caller hands
    /// it to the taskbar icon, which owns it from then on.
    /// </summary>
    public System.Drawing.Icon? Update(Brush? accent, Brush? textOnAccent)
    {
        var colors = ColorsFrom(accent, textOnAccent);
        if (_drawn == colors)
        {
            return null;
        }

        var icon = Create(colors.Background, colors.Foreground, RequiredSize());
        _drawn = colors;
        return icon;
    }

    /// <summary>The colors to draw with: the theme's, or the first swatch with a readable text color.</summary>
    internal static (Color Background, Color Foreground) ColorsFrom(Brush? accent, Brush? textOnAccent)
    {
        var background = accent is SolidColorBrush solidAccent ? solidAccent.Color : FallbackAccent;
        var foreground = textOnAccent is SolidColorBrush solidText ? solidText.Color : ContrastingText(background);
        return (background, foreground);
    }

    /// <summary>Black or white, whichever reads better on <paramref name="background"/>.</summary>
    internal static Color ContrastingText(Color background)
    {
        // Rec. 709 luma; the midpoint is good enough for one bold letter.
        var luma = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B);
        return luma > 128 ? Colors.Black : Colors.White;
    }

    /// <summary>The small-icon size in pixels, which is what the notification area shows.</summary>
    internal static int RequiredSize() => Math.Max(16, IconUtilities.GetRequiredCustomIconSize(largeIcon: false).Width);

    /// <summary>A square icon of <paramref name="size"/> pixels that owns its handle.</summary>
    internal static System.Drawing.Icon Create(Color background, Color foreground, int size) =>
        FromIco(ToIco(RenderPng(background, foreground, size), size), size);

    /// <summary>
    /// Reads an icon from ICO bytes. An icon read this way owns its handle, so disposing it destroys the
    /// handle; <c>Icon.FromHandle</c> would not, which is the leak this class exists to avoid.
    /// </summary>
    internal static System.Drawing.Icon FromIco(byte[] ico, int size)
    {
        using var stream = new MemoryStream(ico, writable: false);
        return new System.Drawing.Icon(stream, size, size);
    }

    /// <summary>Renders the icon with WPF, so no GDI+ is involved, and encodes it as PNG.</summary>
    internal static byte[] RenderPng(Color background, Color foreground, int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var corner = size / 8.0;
            context.DrawRoundedRectangle(Frozen(background), null, new Rect(0, 0, size, size), corner, corner);

            var text = new FormattedText(
                "O",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                size * 0.8,
                Brushes.Black,
                pixelsPerDip: 1.0);
            var glyph = text.BuildGeometry(default);
            var bounds = glyph.Bounds;
            context.PushTransform(new TranslateTransform(((size - bounds.Width) / 2) - bounds.X, ((size - bounds.Height) / 2) - bounds.Y));
            context.DrawGeometry(Frozen(foreground), null, glyph);
            context.Pop();
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }

    /// <summary>Wraps a PNG in a one-image ICO file; Windows reads PNG-compressed icon images.</summary>
    internal static byte[] ToIco(byte[] png, int size)
    {
        const int headerLength = 6 + 16;
        var dimension = (byte)(size >= 256 ? 0 : size);
        using var ico = new MemoryStream(headerLength + png.Length);
        using (var writer = new BinaryWriter(ico, Encoding.ASCII, leaveOpen: true))
        {
            // ICONDIR: reserved, type 1 (icon), one image.
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);

            // ICONDIRENTRY: width, height (0 means 256), no palette, reserved, 1 plane, 32 bits per
            // pixel, then the image's length and where it starts.
            writer.Write(dimension);
            writer.Write(dimension);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(headerLength);
            writer.Write(png);
        }

        return ico.ToArray();
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
