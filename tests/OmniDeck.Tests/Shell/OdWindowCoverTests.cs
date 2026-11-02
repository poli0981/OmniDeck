using System.Windows;
using OmniDeck.App.Shell;
using OmniDeck.Tests.TestSupport;
using Wpf.Ui.Controls;

namespace OmniDeck.Tests.Shell;

public sealed class OdWindowCoverTests
{
    // A monitor left of the primary one: its origin is negative.
    private static readonly Int32Rect LeftMonitor = new(-1920, 0, 1920, 1080);

    [Fact]
    public void Cover_FillsTheMonitorOnTopWithSquareCorners() => OdSta.Run(() =>
    {
        var window = NewWindow();
        var cover = new OdWindowCover(window);

        Assert.True(cover.Cover(LeftMonitor));

        Assert.True(cover.IsCovering);
        Assert.Equal((-1920d, 0d, 1920d, 1080d), (window.Left, window.Top, window.Width, window.Height));
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.True(window.Topmost);
        Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
        Assert.Equal(WindowCornerPreference.DoNotRound, window.WindowCornerPreference);
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public void Uncover_PutsTheWindowBackAsItWas() => OdSta.Run(() =>
    {
        var window = NewWindow();
        window.WindowState = WindowState.Maximized;
        var cover = new OdWindowCover(window);
        cover.Cover(LeftMonitor);

        Assert.True(cover.Uncover());

        Assert.False(cover.IsCovering);
        Assert.Equal((100d, 50d, 800d, 600d), (window.Left, window.Top, window.Width, window.Height));
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.False(window.Topmost);
        Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
        Assert.Equal(WindowCornerPreference.Round, window.WindowCornerPreference);
        Assert.False(cover.Uncover());
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public void Cover_RefusesAnEmptyMonitor_AndKeepsTheFirstPlacement() => OdSta.Run(() =>
    {
        var window = NewWindow();
        var cover = new OdWindowCover(window);

        Assert.False(cover.Cover(Int32Rect.Empty));
        Assert.False(cover.IsCovering);
        Assert.Equal(100d, window.Left);

        cover.Cover(LeftMonitor);
        Assert.False(cover.Cover(new Int32Rect(0, 0, 2560, 1440)));
        Assert.Equal(-1920d, window.Left);

        cover.Uncover();
        Assert.Equal((100d, 50d, 800d, 600d), (window.Left, window.Top, window.Width, window.Height));
        window.Close();
        return Task.CompletedTask;
    });

    private static FluentWindow NewWindow() => new()
    {
        Left = 100,
        Top = 50,
        Width = 800,
        Height = 600,
        ResizeMode = ResizeMode.CanResize,
        WindowCornerPreference = WindowCornerPreference.Round,
    };
}
