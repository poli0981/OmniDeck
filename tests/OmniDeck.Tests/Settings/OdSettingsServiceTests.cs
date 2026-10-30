using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Settings;

public sealed class OdSettingsServiceTests
{
    [Fact]
    public void NoFile_GivesDefaults()
    {
        using var temp = new OdTempDirectory();

        var service = new OdSettingsService(new OdDataRoot(temp.Path), Logger.None);

        Assert.Equal(new OdSettings(), service.Current);
        Assert.Equal("Standard", service.Current.Preset);
        Assert.True(service.Current.CloseToTray);
    }

    [Fact]
    public void Update_SavesAtOnce_AndANewServiceReadsItBack()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Path);
        var service = new OdSettingsService(root, Logger.None);
        OdSettings? raised = null;
        service.Changed += (_, e) => raised = e.Settings;

        service.Update(s => s with { Theme = OdThemeMode.Dark, Preset = "Dynamic" });

        Assert.Equal(OdThemeMode.Dark, raised?.Theme);
        var reloaded = new OdSettingsService(root, Logger.None).Current;
        Assert.Equal(OdThemeMode.Dark, reloaded.Theme);
        Assert.Equal("Dynamic", reloaded.Preset);
    }

    [Fact]
    public void Update_WithNoChange_DoesNotRaise()
    {
        using var temp = new OdTempDirectory();
        var service = new OdSettingsService(new OdDataRoot(temp.Path), Logger.None);
        var raised = 0;
        service.Changed += (_, _) => raised++;

        service.Update(s => s with { });

        Assert.Equal(0, raised);
        Assert.False(File.Exists(temp.Combine("settings.json")));
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Path);
        File.WriteAllText(root.SettingsFile, "{ broken");

        var service = new OdSettingsService(root, Logger.None);

        Assert.Equal(new OdSettings(), service.Current);
        Assert.Single(Directory.GetFiles(temp.Path, "settings.corrupt-*.json"));
    }

    [Theory]
    [InlineData("comfort", "Comfort")]
    [InlineData("Unknown", "Standard")]
    [InlineData("", "Standard")]
    public void Normalized_RepairsThePreset(string stored, string expected)
    {
        Assert.Equal(expected, new OdSettings { Preset = stored }.Normalized().Preset);
    }

    [Fact]
    public void Normalized_RepairsUnknownThemesAndBlankLanguage()
    {
        var repaired = new OdSettings { Theme = (OdThemeMode)42, Language = "  " }.Normalized();

        Assert.Equal(OdThemeMode.System, repaired.Theme);
        Assert.Equal("", repaired.Language);
    }

    [Fact]
    public void LooksLike_IgnoresEverythingButTheAppearance()
    {
        var settings = new OdSettings();

        Assert.True(settings.LooksLike(settings with { PinnedModules = ["clock"], Language = "vi", CloseToTray = false }));
        Assert.False(settings.LooksLike(settings with { Theme = OdThemeMode.Dark }));
        Assert.False(settings.LooksLike(settings with { Preset = "Comfort" }));
        Assert.False(settings.LooksLike(settings with { FontScale = 1.2 }));
        Assert.False(settings.LooksLike(settings with { MotionEnabled = false }));
        Assert.False(settings.LooksLike(settings with { AccentColor = "#FF8800" }));
    }

    [Fact]
    public void Normalized_DropsBlankAndRepeatedPins_AndKeepsACleanListAsIs()
    {
        IReadOnlyList<string> clean = ["clock", "timer"];

        var repaired = new OdSettings { PinnedModules = ["clock", " timer ", "", "clock"] }.Normalized();

        Assert.Equal(new[] { "clock", "timer" }, repaired.PinnedModules);
        Assert.Same(clean, new OdSettings { PinnedModules = clean }.Normalized().PinnedModules);
    }

    [Fact]
    public void PinnedModules_KeepTheirOrder_AndANullListReadsAsEmpty()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Path);

        new OdSettingsService(root, Logger.None).Update(s => s with { PinnedModules = ["timer", "clock"] });
        Assert.Equal(new[] { "timer", "clock" }, new OdSettingsService(root, Logger.None).Current.PinnedModules);

        File.WriteAllText(root.SettingsFile, """{ "pinnedModules": null }""");
        Assert.Empty(new OdSettingsService(root, Logger.None).Current.PinnedModules);
    }
}
