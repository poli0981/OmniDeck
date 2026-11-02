namespace OmniDeck.App.Shell;

/// <summary>Ends the app on purpose, as opposed to closing its window into the tray.</summary>
public interface IOdAppLifetime
{
    /// <summary>Set once <see cref="QuitAsync"/> starts: window closing must not turn into hiding.</summary>
    bool IsQuitting { get; }

    /// <summary>Closes the open modules, then ends the app. Calls after the first do nothing.</summary>
    Task QuitAsync();
}
