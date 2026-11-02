using System.IO;

namespace OmniDeck.Modules.Media;

/// <summary>The files the media module plays (docs/SPEC.md §3.6): local audio and video, and .m3u playlists.</summary>
internal static class OdMediaFiles
{
    /// <summary>The extensions it accepts, audio first.</summary>
    public static readonly string[] Extensions =
    [
        ".mp3", ".flac", ".wav", ".ogg", ".opus", ".m4a", ".aac", ".wma",
        ".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".m4v", ".ts",
    ];

    public static bool IsMedia(string path) => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static bool IsPlaylist(string path) =>
        Path.GetExtension(path) is { } extension
        && (string.Equals(extension, ".m3u", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase));

    /// <summary>The media files directly in <paramref name="folder"/>, by name.</summary>
    public static IReadOnlyList<string> InFolder(string folder) =>
        [.. Directory.EnumerateFiles(folder).Where(IsMedia).Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The file dialog's filter: every accepted extension, then everything.</summary>
    public static string Filter(string mediaLabel, string allLabel) =>
        $"{mediaLabel}|{string.Join(';', Extensions.Select(extension => "*" + extension))}|{allLabel}|*.*";
}
