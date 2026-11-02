using System.Text.Json;
using Dapper;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Media;

/// <summary>Where the media module keeps its queue between sessions: the <c>media_playlists</c> table.</summary>
internal sealed class OdMediaStore(OdDatabase database)
{
    private const string Queue = "queue";

    /// <summary>The queue as last saved, and where playback stood; empty the first time.</summary>
    public (OdMediaQueue Queue, TimeSpan Position) Load()
    {
        using var connection = database.Open();
        var row = connection.QuerySingleOrDefault<(string Items, long Current, long PositionMs)>(
            "SELECT items, current, position_ms FROM media_playlists WHERE name = @name", new { name = Queue });
        if (row.Items is null)
        {
            return (new OdMediaQueue([], 0), TimeSpan.Zero);
        }

        var items = JsonSerializer.Deserialize(row.Items, OdModulesJsonContext.Default.StringArray) ?? [];
        return (new OdMediaQueue(items, (int)row.Current), TimeSpan.FromMilliseconds(row.PositionMs));
    }

    public void Save(OdMediaQueue queue, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(queue);
        using var connection = database.Open();
        connection.Execute(
            """
            INSERT INTO media_playlists (name, items, current, position_ms) VALUES (@name, @items, @current, @position)
            ON CONFLICT (name) DO UPDATE SET items = excluded.items, current = excluded.current, position_ms = excluded.position_ms
            """,
            new
            {
                name = Queue,
                items = JsonSerializer.Serialize([.. queue.Items], OdModulesJsonContext.Default.StringArray),
                current = queue.Current,
                position = (long)position.TotalMilliseconds,
            });
    }
}
