using System.Diagnostics;

namespace OmniDeck.App.Shell;

/// <summary>Opens links and folders the user asked for, with the default browser or File Explorer.</summary>
internal static class OdExternal
{
    public static void Open(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Start(uri.AbsoluteUri);
    }

    public static void OpenFolder(string path) => Start(path);

    /// <summary>Opens File Explorer on the folder holding <paramref name="path"/>, with the file selected.</summary>
    public static void ShowFile(string path)
    {
        var explorer = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        using var process = Process.Start(new ProcessStartInfo(explorer) { ArgumentList = { "/select," + path } });
    }

    private static void Start(string target)
    {
        using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
