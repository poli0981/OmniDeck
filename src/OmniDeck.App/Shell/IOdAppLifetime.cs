namespace OmniDeck.App.Shell;

/// <summary>Ends the app on purpose, as opposed to closing its window into the tray.</summary>
public interface IOdAppLifetime
{
    /// <summary>Set once <see cref="Quit"/> runs: window closing must not turn into hiding.</summary>
    bool IsQuitting { get; }

    void Quit();
}
