using System.Security.Cryptography;
using System.Text;
using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Core.Security;

/// <summary>
/// The secret store: one file per secret in <see cref="OdDataRoot.SecretsDirectory"/>, encrypted with
/// DPAPI for the current Windows user (CLAUDE.md rule 4), so another account or another PC cannot read
/// it. It is written atomically, and the plain text is wiped from memory once encrypted. A file that
/// no longer decrypts (a profile restored on another PC, say) reads as no secret, and the user is
/// asked again. Nothing about a secret's value is ever logged.
/// </summary>
public sealed class OdDpapiSecretStore(OdDataRoot dataRoot, ILogger log) : IOdSecretStore
{
    // Ties the ciphertext to OmniDeck: another program running as the same user cannot decrypt it by
    // accident with ProtectedData alone.
    private static readonly byte[] Entropy = "OmniDeck.Secret.v1"u8.ToArray();

    private readonly ILogger _log = log.ForContext<OdDpapiSecretStore>();

    public string? Read(string name)
    {
        var path = PathOf(name);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException ex)
        {
            // The exception says why DPAPI refused, never anything of the secret.
            _log.Warning(ex, "The stored secret {Name} could not be decrypted; it will be asked for again", name);
            return null;
        }
        finally
        {
            if (plain is not null)
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
    }

    public void Write(string name, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var path = PathOf(name);
        var plain = Encoding.UTF8.GetBytes(secret);
        byte[] cipher;
        try
        {
            cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        Directory.CreateDirectory(dataRoot.SecretsDirectory);
        var temp = Path.Combine(dataRoot.SecretsDirectory, $".{name}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, cipher);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public void Delete(string name) => File.Delete(PathOf(name));

    /// <summary>The file of <paramref name="name"/>: lowercase letters, digits and dashes only, so a name never leaves the folder.</summary>
    private string PathOf(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw new ArgumentException("A secret's name is lowercase letters, digits and dashes.", nameof(name));
        }

        return Path.Combine(dataRoot.SecretsDirectory, name + ".bin");
    }
}
