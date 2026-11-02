namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Shows the user that something failed: an in-window dialog once the main window is up
/// (<see cref="OdDialogErrorPresenter"/>), a message box before that.
/// </summary>
internal interface IOdErrorPresenter
{
    void Show(string message);
}
