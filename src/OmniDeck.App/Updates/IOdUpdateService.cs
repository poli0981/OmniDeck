namespace OmniDeck.App.Updates;

/// <summary>OmniDeck's updates from its GitHub releases (docs/ARCHITECTURE.md §7).</summary>
public interface IOdUpdateService
{
    /// <summary>Whether this copy was installed by Velopack; development builds cannot update.</summary>
    bool IsInstalled { get; }

    /// <summary>The newer version available, or <see langword="null"/> when this one is the latest.</summary>
    Task<string?> CheckAsync(CancellationToken cancellationToken);

    /// <summary>Downloads the update found by the last check, reporting percent done.</summary>
    Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken);

    /// <summary>Applies the downloaded update and restarts OmniDeck.</summary>
    void ApplyAndRestart();
}
