using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Storage;

public sealed class OdDataRootTests
{
    [Fact]
    public void Default_IsTheDataFolderUnderLocalAppData_NotVelopacksInstallFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(local, "OmniDeck.Data"), OdDataRoot.Default.Root);
    }

    [Fact]
    public void Paths_AllLiveUnderTheRoot()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Path);

        Assert.All(
            [root.SettingsFile, root.DatabaseFile, root.LogsDirectory, root.ReportsDirectory, root.WebView2Directory],
            path => Assert.StartsWith(temp.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("OmniDeck")]
    [InlineData("omnideck")]
    public void Constructor_RejectsVelopacksInstallFolder(string name)
    {
        using var temp = new OdTempDirectory();

        Assert.Throws<ArgumentException>(() => new OdDataRoot(temp.Combine(name)));
    }

    [Fact]
    public void EnsureCreated_ThenTryDelete_LeavesNothing()
    {
        using var temp = new OdTempDirectory();
        var root = new OdDataRoot(temp.Combine("OmniDeck.Data"));

        root.EnsureCreated();
        File.WriteAllText(root.SettingsFile, "{}");
        Assert.True(Directory.Exists(root.LogsDirectory));

        Assert.True(root.TryDelete());
        Assert.False(Directory.Exists(root.Root));
        Assert.True(root.TryDelete());
    }
}
