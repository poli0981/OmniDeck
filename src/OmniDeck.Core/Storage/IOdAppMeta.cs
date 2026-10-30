namespace OmniDeck.Core.Storage;

/// <summary>App-wide facts kept in the database rather than in settings, such as the legal acknowledgment.</summary>
public interface IOdAppMeta
{
    string? Get(string key);

    void Set(string key, string value);
}
