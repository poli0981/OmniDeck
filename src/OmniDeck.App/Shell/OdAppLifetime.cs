using System.Windows;

namespace OmniDeck.App.Shell;

/// <summary><see cref="IOdAppLifetime"/> over the WPF application, whose shutdown mode is explicit.</summary>
internal sealed class OdAppLifetime : IOdAppLifetime
{
    public bool IsQuitting { get; private set; }

    public void Quit()
    {
        IsQuitting = true;
        Application.Current?.Shutdown();
    }
}
