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
}
