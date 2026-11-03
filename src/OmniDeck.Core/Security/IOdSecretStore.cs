namespace OmniDeck.Core.Security;

/// <summary>
/// Secrets the user gives OmniDeck, such as their Twelve Data key (SECURITY.md): stored encrypted
/// for the signed-in Windows user, never in settings, exports, logs or bug reports.
/// </summary>
public interface IOdSecretStore
{
    /// <summary>The secret called <paramref name="name"/>; null when there is none, or when it cannot be decrypted.</summary>
    string? Read(string name);

    void Write(string name, string secret);

    void Delete(string name);
}
