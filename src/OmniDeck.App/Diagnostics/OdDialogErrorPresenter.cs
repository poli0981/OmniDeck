using OmniDeck.App.Shell;
using OmniDeck.Core.Localization;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Diagnostics;

/// <summary>Shows errors in an in-window dialog, or in a message box while no dialog can show.</summary>
internal sealed class OdDialogErrorPresenter(OdDialogs dialogs, OdMessageBoxErrorPresenter fallback, OdBugReportRequests reports) : IOdErrorPresenter
{
    public void Show(string message)
    {
        if (!dialogs.CanShow)
        {
            fallback.Show(message);
            return;
        }

        _ = ShowAsync(message);
    }

    private async Task ShowAsync(string message)
    {
        var answer = await dialogs.ShowAsync(new ContentDialog
        {
            Title = OdLocalization.Get("Error_Title"),
            Content = message,
            SecondaryButtonText = OdLocalization.Get("Error_Report"),
            CloseButtonText = OdLocalization.Get("Dialog_OK"),
            DefaultButton = ContentDialogButton.Close,
        });
        if (answer == ContentDialogResult.Secondary)
        {
            reports.Request(message);
        }
    }
}
