using OmniDeck.Core.Security;

namespace OmniDeck.Tests.Stocks;

/// <summary>A secret store in memory, for tests that must not touch DPAPI or the disk.</summary>
public sealed class OdMemorySecrets : IOdSecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public string? Read(string name) => _secrets.GetValueOrDefault(name);

    public void Write(string name, string secret) => _secrets[name] = secret;

    public void Delete(string name) => _secrets.Remove(name);
}
