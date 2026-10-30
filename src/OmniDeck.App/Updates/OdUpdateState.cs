namespace OmniDeck.App.Updates;

/// <summary>Where an update check stands, as About shows it.</summary>
public enum OdUpdateState
{
    /// <summary>Nothing checked yet in this session.</summary>
    Idle,

    Checking,

    UpToDate,

    /// <summary>A newer release exists and can be downloaded.</summary>
    Available,

    Downloading,

    /// <summary>The check or the download failed.</summary>
    Failed,

    /// <summary>A development build: Velopack only updates installed copies.</summary>
    NotInstalled,
}
