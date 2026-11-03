using System.Windows.Media;
using OmniDeck.App.Theming;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Core.Theming;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;
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
    public void APreview_HasThePresetsTokens_AtTheUsersFontScale()
    {
        using var temp = new OdTempDirectory();
        var settings = new OdSettingsService(new OdDataRoot(temp.Path), Logger.None);
        settings.Update(s => s with { Preset = "Dynamic", FontScale = 1.2 });
        using var engine = new OdThemeEngine(settings, Logger.None);

        var preview = engine.Preview(OdPreset.Comfort);

        Assert.Equal("Comfort", preview[OdTokenKeys.PresetMarker]);
        Assert.Equal(22.8, preview[OdTokenKeys.FontBase]);
        Assert.Equal(44.0, preview[OdTokenKeys.HitTargetMin]);
    }

    [Fact]
    public void TheThemeIsAppliedAgain_OnlyWhenWindowsLookChanges()
    {
        using var temp = new OdTempDirectory();
        using var engine = new OdThemeEngine(new OdSettingsService(new OdDataRoot(temp.Path), Logger.None), Logger.None);
        var look = new OdSystemLook(SystemTheme.Dark, Colors.SteelBlue, HighContrast: false, Animations: true);

        Assert.True(engine.NoteSystemLook(look));
        Assert.False(engine.NoteSystemLook(look));
        Assert.True(engine.NoteSystemLook(look with { Accent = Colors.IndianRed }));
        Assert.True(engine.NoteSystemLook(look with { Accent = Colors.IndianRed, Animations = false }));
        Assert.False(engine.NoteSystemLook(look with { Accent = Colors.IndianRed, Animations = false }));
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
