using System.Windows;
using OmniDeck.Core.Localization;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace OmniDeck.App.Shell;

/// <summary>
/// In-window content dialogs, one at a time. Callers check <see cref="CanShow"/> and fall back (a
/// message box, or a safe default) before the main window is up or while another dialog is open.
/// </summary>
public sealed class OdDialogs(IContentDialogService service)
{
    private bool _hostReady;
    private int _open;

    /// <summary>Whether a dialog is showing.</summary>
    public bool IsOpen => _open > 0;

    /// <summary>Whether a dialog can show now: the main window is visible and no other dialog is open.</summary>
    public bool CanShow => _hostReady && !IsOpen && Application.Current?.MainWindow is { IsVisible: true };

    /// <summary>Called once by the main window with its dialog host.</summary>
    public void SetHost(ContentDialogHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        service.SetDialogHost(host);
        _hostReady = true;
    }

    /// <summary>Asks before something that cannot be undone; false when declined, or when no dialog can show.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirm)
    {
        if (!CanShow)
        {
            return false;
        }

        var result = await ShowAsync(new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirm,
            CloseButtonText = OdLocalization.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        });
        return result == ContentDialogResult.Primary;
    }

    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        _open++;
        try
        {
            return await service.ShowAsync(dialog, CancellationToken.None);
        }
        finally
        {
            _open--;
        }
    }
}
