using System.Windows;
using OmniDeck.Core.Modules;

namespace OmniDeck.App.Shell;

/// <summary>
/// <see cref="IOdAppLifetime"/> over the WPF application, whose shutdown mode is explicit. The modules
/// close before the dispatcher stops, while they can still save their state and release what they hold.
/// </summary>
internal sealed class OdAppLifetime(OdModuleHost host) : IOdAppLifetime
{
    public bool IsQuitting { get; private set; }

    public async Task QuitAsync()
    {
        if (IsQuitting)
        {
            return;
        }

        IsQuitting = true;
        await host.CloseAllAsync();
        Application.Current?.Shutdown();
    }
}
