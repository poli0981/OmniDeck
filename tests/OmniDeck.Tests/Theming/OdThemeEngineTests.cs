using System.Windows.Media;
using OmniDeck.App.Theming;
using OmniDeck.Core.Settings;
using Wpf.Ui.Appearance;

namespace OmniDeck.Tests.Theming;

public sealed class OdThemeEngineTests
{
    [Fact]
    public void FollowingWindows_UsesTheWindowsAccent()
    {
        var (followSystem, _, accent) = OdThemeEngine.Choose(OdThemeMode.System, "", highContrast: false, () => ApplicationTheme.Dark);

        Assert.True(followSystem);
        Assert.Null(accent);
    }

    [Theory]
    [InlineData(OdThemeMode.Light, ApplicationTheme.Light)]
    [InlineData(OdThemeMode.Dark, ApplicationTheme.Dark)]
    public void AChosenTheme_GetsTheUsersAccentWorkedOutForIt(OdThemeMode mode, ApplicationTheme expected)
    {
        var (followSystem, theme, accent) = OdThemeEngine.Choose(mode, "#E74856", highContrast: false, () => throw new InvalidOperationException("Windows' theme is not needed"));

        Assert.False(followSystem);
        Assert.Equal(expected, theme);
        Assert.Equal(Color.FromRgb(0xE7, 0x48, 0x56), accent);
    }

    [Fact]
    public void TheUsersAccentOnWindowsTheme_IsWorkedOutForWindowsTheme()
    {
        var (followSystem, theme, accent) = OdThemeEngine.Choose(OdThemeMode.System, "#10893E", highContrast: false, () => ApplicationTheme.Dark);

        Assert.True(followSystem);
        Assert.Equal(ApplicationTheme.Dark, theme);
        Assert.Equal(Color.FromRgb(0x10, 0x89, 0x3E), accent);
    }

    [Fact]
    public void HighContrast_OverridesTheUsersThemeAndAccent()
    {
        var (followSystem, theme, accent) = OdThemeEngine.Choose(OdThemeMode.Light, "#10893E", highContrast: true, () => ApplicationTheme.HighContrast);

        Assert.True(followSystem);
        Assert.Equal(ApplicationTheme.HighContrast, theme);
        Assert.Null(accent);
    }
}
