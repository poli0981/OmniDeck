using System.IO;
using System.Text;

namespace OmniDeck.Modules.Media;

/// <summary>Reads and writes .m3u playlists: one path per line, <c>#</c> lines are comments or extended info.</summary>
internal static class OdM3u
{
    /// <summary>The paths in <paramref name="text"/>; a relative one is taken from <paramref name="folder"/>, and URLs are skipped.</summary>
    public static IReadOnlyList<string> Parse(string text, string folder)
    {
        ArgumentNullException.ThrowIfNull(text);
        var paths = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.Contains("://", StringComparison.Ordinal))
            {
                continue;
            }

            paths.Add(Path.GetFullPath(Path.IsPathRooted(line) ? line : Path.Combine(folder, line)));
        }

        return paths;
    }

    /// <summary>An extended .m3u holding <paramref name="paths"/>, each named after its file.</summary>
    public static string Write(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var text = new StringBuilder("#EXTM3U\n");
        foreach (var path in paths)
        {
            text.Append("#EXTINF:-1,").Append(Path.GetFileNameWithoutExtension(path)).Append('\n').Append(path).Append('\n');
        }

        return text.ToString();
    }
}
