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
            [root.SettingsFile, root.DatabaseFile, root.LogsDirectory, root.ReportsDirectory, root.WebView2Directory, root.CatalogFile],
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Choose_WithoutAnOverride_IsTheRealRoot(string? overridePath)
    {
        var root = OdDataRoot.Choose(overridePath);

        Assert.Same(OdDataRoot.Default, root);
        Assert.True(root.IsDefault);
        Assert.Null(root.InstanceSuffix);
    }

    [Fact]
    public void Choose_WithAnOverride_UsesIt_AndGetsAnInstanceOfItsOwn()
    {
        using var temp = new OdTempDirectory();
        var path = temp.Combine("OmniDeck.Dev");

        var root = OdDataRoot.Choose(path);

        Assert.Equal(path, root.Root, StringComparer.OrdinalIgnoreCase);
        Assert.False(root.IsDefault);
        Assert.Matches("^\\.[0-9A-F]{8}$", root.InstanceSuffix);
        Assert.Equal(root.InstanceSuffix, OdDataRoot.Choose(path.ToUpperInvariant() + Path.DirectorySeparatorChar).InstanceSuffix, StringComparer.Ordinal);
        Assert.NotEqual(root.InstanceSuffix, OdDataRoot.Choose(temp.Combine("OmniDeck.Other")).InstanceSuffix, StringComparer.Ordinal);
    }

    [Fact]
    public void Choose_StillRejectsVelopacksInstallFolder()
    {
        using var temp = new OdTempDirectory();

        Assert.Throws<ArgumentException>(() => OdDataRoot.Choose(temp.Combine("OmniDeck")));
    }

    [Fact]
    public void Choose_TheRealPathItself_CountsAsTheRealRoot()
    {
        var root = OdDataRoot.Choose(OdDataRoot.Default.Root + Path.DirectorySeparatorChar);

        Assert.True(root.IsDefault);
        Assert.Null(root.InstanceSuffix);
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
