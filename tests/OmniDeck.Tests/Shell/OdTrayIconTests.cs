using System.Windows.Media;
using OmniDeck.App.Shell;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Shell;

public sealed class OdTrayIconTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void Create_GivesAnIconOfTheRequestedSize(int size) => OdSta.Run(() =>
    {
        using var icon = OdTrayIcon.Create(Colors.SteelBlue, Colors.White, size);

        Assert.NotEqual(IntPtr.Zero, icon.Handle);
        Assert.Equal(size, icon.Width);
        Assert.Equal(size, icon.Height);
        return Task.CompletedTask;
    });

    [Fact]
    public void Update_DrawsAgainOnlyWhenTheColorsChange() => OdSta.Run(() =>
    {
        var tray = new OdTrayIcon();
        var white = new SolidColorBrush(Colors.White);

        using var first = tray.Update(new SolidColorBrush(Colors.SteelBlue), white);
        using var same = tray.Update(new SolidColorBrush(Colors.SteelBlue), white);
        using var changed = tray.Update(new SolidColorBrush(Colors.IndianRed), white);

        Assert.NotNull(first);
        Assert.Null(same);
        Assert.NotNull(changed);
        return Task.CompletedTask;
    });

    [Fact]
    public void Icons_FreeTheirHandlesWhenDisposed() => OdSta.Run(() =>
    {
        // A process gets 10,000 GDI objects and each icon holds two bitmaps, so icons that never free
        // their handles (Icon.FromHandle, as H.NotifyIcon's generated icons do) fail well before the end.
        var ico = OdTrayIcon.ToIco(OdTrayIcon.RenderPng(Colors.SteelBlue, Colors.White, 32), 32);
        for (var i = 0; i < 6_000; i++)
        {
            using var icon = OdTrayIcon.FromIco(ico, 32);
            Assert.NotEqual(IntPtr.Zero, icon.Handle);
        }

        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(32, 32)]
    [InlineData(256, 0)]
    public void ToIco_WritesAOneImageIconAroundThePng(int size, byte dimension)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var ico = OdTrayIcon.ToIco(png, size);

        Assert.Equal(new byte[] { 0, 0, 1, 0, 1, 0, dimension, dimension }, ico[..8]);
        Assert.Equal(png.Length, BitConverter.ToInt32(ico, 14));
        Assert.Equal(22, BitConverter.ToInt32(ico, 18));
        Assert.Equal(png, ico[22..]);
    }

    [Fact]
    public void MissingBrushes_FallBackToTheFirstSwatchWithReadableText()
    {
        var (background, foreground) = OdTrayIcon.ColorsFrom(null, null);

        Assert.Equal(Color.FromRgb(0x00, 0x67, 0xC0), background);
        Assert.Equal(Colors.White, foreground);
    }

    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF, false)]
    [InlineData(0xFF, 0xD7, 0x00, false)]
    [InlineData(0x10, 0x89, 0x3E, true)]
    [InlineData(0x00, 0x00, 0x00, true)]
    public void ContrastingText_IsBlackOnLightColorsAndWhiteOnDarkOnes(byte red, byte green, byte blue, bool white) =>
        Assert.Equal(white ? Colors.White : Colors.Black, OdTrayIcon.ContrastingText(Color.FromRgb(red, green, blue)));
}
