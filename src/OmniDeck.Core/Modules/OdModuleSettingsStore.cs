using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Dapper;
using OmniDeck.Core.Storage;

namespace OmniDeck.Core.Modules;

/// <summary><see cref="IOdModuleSettings"/> for one module over the <c>module_settings</c> table.</summary>
public sealed class OdModuleSettingsStore(OdDatabase database, string moduleId) : IOdModuleSettings
{
    public T? Get<T>(JsonTypeInfo<T> typeInfo)
        where T : class
    {
        using var connection = database.Open();
        var json = connection.ExecuteScalar<string?>("SELECT json FROM module_settings WHERE module_id = @moduleId", new { moduleId });
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Set<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO module_settings (module_id, json) VALUES (@moduleId, @json) ON CONFLICT (module_id) DO UPDATE SET json = excluded.json",
            new { moduleId, json });
    }
}
