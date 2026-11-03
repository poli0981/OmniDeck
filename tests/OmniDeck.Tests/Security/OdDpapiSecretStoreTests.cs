using OmniDeck.Core.Security;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Security;

public sealed class OdDpapiSecretStoreTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly OdDataRoot _root;
    private readonly OdDpapiSecretStore _store;

    public OdDpapiSecretStoreTests()
    {
        _root = new OdDataRoot(_temp.Path);
        _store = new OdDpapiSecretStore(_root, Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ASecret_RoundTrips_AndIsNotStoredAsPlainText()
    {
        _store.Write("twelvedata", "my-secret-key-123");

        Assert.Equal("my-secret-key-123", _store.Read("twelvedata"));
        var file = Path.Combine(_root.SecretsDirectory, "twelvedata.bin");
        Assert.DoesNotContain("my-secret-key-123", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file)), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_root.SecretsDirectory, "*.tmp"));

        _store.Write("twelvedata", "another");
        Assert.Equal("another", _store.Read("twelvedata"));
    }

    [Fact]
    public void AMissingOrDeletedSecret_ReadsAsNone()
    {
        Assert.Null(_store.Read("twelvedata"));

        _store.Write("twelvedata", "key");
        _store.Delete("twelvedata");
        _store.Delete("twelvedata");

        Assert.Null(_store.Read("twelvedata"));
    }

    [Fact]
    public void ASecretThatNoLongerDecrypts_ReadsAsNone()
    {
        Directory.CreateDirectory(_root.SecretsDirectory);
        File.WriteAllBytes(Path.Combine(_root.SecretsDirectory, "twelvedata.bin"), [1, 2, 3, 4, 5]);

        Assert.Null(_store.Read("twelvedata"));
    }

    [Theory]
    [InlineData("../settings")]
    [InlineData("Twelve Data")]
    [InlineData("a/b")]
    [InlineData("")]
    public void Names_StayInTheSecretsFolder(string name) =>
        Assert.ThrowsAny<ArgumentException>(() => _store.Write(name, "key"));
}
