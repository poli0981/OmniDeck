namespace OmniDeck.App.Shell;

/// <summary>A command the palette offers that the main window carries out.</summary>
public enum OdShellCommand
{
    /// <summary>Check for updates, on Settings → About.</summary>
    CheckForUpdates,

    /// <summary>Open the bug report form.</summary>
    ReportBug,

    /// <summary>Open the data folder in File Explorer.</summary>
    OpenDataFolder,
}
