using System.Windows;
using OmniDeck.Core.Modules;
using OmniDeck.Web.Runtime;

namespace OmniDeck.App.Shell;

/// <summary>
/// <see cref="IOdAppLifetime"/> over the WPF application, whose shutdown mode is explicit. The modules
/// close before the dispatcher stops, while they can still save their state and release what they hold,
/// and the shared WebView2 browser process gets a moment to exit and write its cookies out.
/// </summary>
internal sealed class OdAppLifetime(OdModuleHost host, OdWebRuntime web) : IOdAppLifetime
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
        await web.WaitForExitAsync();
        Application.Current?.Shutdown();
    }
}
