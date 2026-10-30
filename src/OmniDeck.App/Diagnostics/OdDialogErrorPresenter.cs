using OmniDeck.App.Shell;
using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Diagnostics;

/// <summary>Shows errors in an in-window dialog, or in a message box while no dialog can show.</summary>
internal sealed class OdDialogErrorPresenter(OdDialogs dialogs, OdMessageBoxErrorPresenter fallback) : IOdErrorPresenter
{
    public void Show(string message)
    {
        if (!dialogs.CanShow)
        {
            fallback.Show(message);
            return;
        }

        _ = dialogs.ShowAsync(new ContentDialog
        {
            Title = OdLocalization.Get("Error_Title"),
            Content = message,
            CloseButtonText = OdLocalization.Get("Dialog_OK"),
            DefaultButton = ContentDialogButton.Close,
        });
    }
}
