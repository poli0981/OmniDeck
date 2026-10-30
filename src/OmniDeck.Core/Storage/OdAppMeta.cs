using Dapper;

namespace OmniDeck.Core.Storage;

/// <summary><see cref="IOdAppMeta"/> over the <c>app_meta</c> table.</summary>
public sealed class OdAppMeta(OdDatabase database) : IOdAppMeta
{
    public string? Get(string key)
    {
        using var connection = database.Open();
        return connection.ExecuteScalar<string?>("SELECT value FROM app_meta WHERE key = @key", new { key });
    }

    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO app_meta (key, value) VALUES (@key, @value) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            new { key, value });
    }
}
