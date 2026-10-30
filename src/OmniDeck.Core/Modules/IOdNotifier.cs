namespace OmniDeck.Core.Modules;

/// <summary>Raises a Windows notification under OmniDeck's name.</summary>
public interface IOdNotifier
{
    void Notify(string title, string message);
}
