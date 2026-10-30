namespace OmniDeck.App.Settings;

/// <summary>Starting OmniDeck when the user signs in to Windows, quietly in the tray.</summary>
public interface IOdStartupRegistration
{
    bool IsEnabled { get; }

    void Set(bool enabled);
}
