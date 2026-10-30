using OmniDeck.Core.Modules;
using Serilog;

namespace OmniDeck.App.Shell;

/// <summary>
/// <see cref="IOdNotifier"/> through the tray icon's balloon, which Windows 11 shows as a toast.
/// WinRT toasts would need a Windows-versioned target framework and its 26 MB projection assembly
/// (docs/spikes/05-theme-swap.md). The main window attaches the icon; until then notifications are
/// only logged.
/// </summary>
public sealed class OdTrayNotifier(ILogger log) : IOdNotifier
{
    private readonly ILogger _log = log.ForContext<OdTrayNotifier>();
    private Action<string, string>? _show;

    public void Attach(Action<string, string> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        _show = show;
    }

    public void Notify(string title, string message)
    {
        if (_show is null)
        {
            _log.Information("Notification before the tray icon exists: {Title}", title);
            return;
        }

        _show(title, message);
    }
}
