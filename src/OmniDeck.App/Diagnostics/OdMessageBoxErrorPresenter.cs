using System.Windows;

namespace OmniDeck.App.Diagnostics;

/// <summary>The fallback presenter: a plain message box, which works before any window exists.</summary>
internal sealed class OdMessageBoxErrorPresenter : IOdErrorPresenter
{
    public void Show(string message) =>
        MessageBox.Show(message, "OmniDeck", MessageBoxButton.OK, MessageBoxImage.Warning);
}
