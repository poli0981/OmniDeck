using System.Globalization;
using System.IO;

namespace OmniDeck.Modules.Media;

/// <summary>Where snapshots go: Pictures\OmniDeck, named after the file and the moment.</summary>
internal static class OdMediaSnapshots
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "OmniDeck");

    public static string PathFor(string folder, string title, DateTimeOffset now)
    {
        var safe = string.Concat(title.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c)).Trim();
        var name = (safe.Length == 0 ? "OmniDeck" : safe) + " " + now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) + ".png";
        return Path.Combine(folder, name);
    }
}
