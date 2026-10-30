namespace OmniDeck.Core.Storage;

/// <summary>
/// The data root, <c>%LocalAppData%\OmniDeck.Data</c>: everything the user owns lives here (settings,
/// database, WebView2 profiles, logs, bug reports). It is a sibling of Velopack's install folder
/// <c>%LocalAppData%\OmniDeck</c>, which is wiped on uninstall and reinstall, so nothing here may ever
/// move into it (docs/ARCHITECTURE.md §5).
/// </summary>
public sealed class OdDataRoot
{
    /// <summary>The data root's folder name under <c>%LocalAppData%</c>.</summary>
    public const string FolderName = "OmniDeck.Data";

    /// <summary>Velopack's install folder name, which the data root must never be.</summary>
    private const string InstallFolderName = "OmniDeck";

    public OdDataRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
        if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(Root)), InstallFolderName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The data root must not be Velopack's install folder.", nameof(root));
        }
    }

    /// <summary>The real data root of the signed-in user.</summary>
    public static OdDataRoot Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName));

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string DatabaseFile => Path.Combine(Root, "omnideck.db");

    public string LogsDirectory => Path.Combine(Root, "logs");

    public string ReportsDirectory => Path.Combine(Root, "reports");

    public string WebView2Directory => Path.Combine(Root, "WebView2");

    /// <summary>Creates the root and the folders the app writes to from the first second.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
    }

    /// <summary>
    /// Deletes the whole data root. Called from Velopack's uninstall hook, as the privacy policy
    /// promises; it must never throw, because a failing hook would leave the uninstall half done.
    /// </summary>
    /// <returns><see langword="true"/> when nothing is left behind.</returns>
    public bool TryDelete()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
