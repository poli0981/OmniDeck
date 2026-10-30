using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Storage;

public sealed class OdJsonFileTests
{
    [Fact]
    public void Write_ThenRead_RoundTrips_AndLeavesNoTempFiles()
    {
        using var temp = new OdTempDirectory();
        var path = temp.Combine("settings.json");
        var settings = new OdSettings { Preset = "Comfort", CloseToTray = false };

        OdJsonFile.Write(path, settings, OdSettingsJsonContext.Default.OdSettings);
        var read = OdJsonFile.Read(path, OdSettingsJsonContext.Default.OdSettings, out var status);

        Assert.Equal(OdJsonReadStatus.Loaded, status);
        Assert.Equal(settings, read);
        Assert.Equal("settings.json", Path.GetFileName(Assert.Single(Directory.GetFiles(temp.Path))));
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        using var temp = new OdTempDirectory();

        var read = OdJsonFile.Read(temp.Combine("nothing.json"), OdSettingsJsonContext.Default.OdSettings, out var status);

        Assert.Null(read);
        Assert.Equal(OdJsonReadStatus.Missing, status);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void Read_CorruptFile_IsSetAside(string content)
    {
        using var temp = new OdTempDirectory();
        var path = temp.Combine("settings.json");
        File.WriteAllText(path, content);

        var read = OdJsonFile.Read(path, OdSettingsJsonContext.Default.OdSettings, out var status);

        Assert.Null(read);
        Assert.Equal(OdJsonReadStatus.Quarantined, status);
        Assert.False(File.Exists(path));
        var quarantined = Assert.Single(Directory.GetFiles(temp.Path, "settings.corrupt-*.json"));
        Assert.Equal(content, File.ReadAllText(quarantined));
    }
}
