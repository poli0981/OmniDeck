namespace OmniDeck.App.Diagnostics;

/// <summary>Shows the user that something failed; the shell swaps in an in-window dialog in WP4.</summary>
internal interface IOdErrorPresenter
{
    void Show(string message);
}
