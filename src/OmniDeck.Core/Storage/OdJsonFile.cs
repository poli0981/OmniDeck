using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace OmniDeck.Core.Storage;

/// <summary>
/// Small JSON files under the data root: written atomically (a temp file next to the target, then a
/// rename), so a crash or power loss never leaves half a file; read leniently, so a corrupt file is
/// set aside for inspection instead of crashing the app.
/// </summary>
public static class OdJsonFile
{
    public static T? Read<T>(string path, JsonTypeInfo<T> typeInfo, out OdJsonReadStatus status)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (!File.Exists(path))
        {
            status = OdJsonReadStatus.Missing;
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var value = JsonSerializer.Deserialize(stream, typeInfo);
            if (value is not null)
            {
                status = OdJsonReadStatus.Loaded;
                return value;
            }
        }
        catch (JsonException)
        {
            // Falls through to the quarantine below.
        }

        Quarantine(path);
        status = OdJsonReadStatus.Quarantined;
        return null;
    }

    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, value, typeInfo);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static void Quarantine(string path)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var target = Path.ChangeExtension(path, $".corrupt-{stamp}.json");
        File.Move(path, target, overwrite: true);
    }
}
