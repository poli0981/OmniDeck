using OmniDeck.Core.Modules;
using Serilog;

namespace OmniDeck.App.Shell;

/// <summary>
/// <see cref="IOdNotifier"/> through the tray icon's balloon, which Windows 11 shows as a toast.
/// WinRT toasts would need a Windows-versioned target framework and its 26 MB projection assembly
/// (docs/spikes/05-theme-swap.md). The main window attaches the icon; until then notifications are
/// only logged. Windows reports a click on the latest balloon only, so a click opens what that
/// notification names.
/// </summary>
public sealed class OdTrayNotifier(ILogger log) : IOdNotifier
{
    private readonly ILogger _log = log.ForContext<OdTrayNotifier>();
    private Action<string, string>? _show;
    private string? _lastTarget;
    private Action? _lastClicked;

    /// <summary>The user clicked the latest notification.</summary>
    public event EventHandler<OdNotificationClickedEventArgs>? Clicked;

    public void Attach(Action<string, string> show)
    {
        ArgumentNullException.ThrowIfNull(show);
        _show = show;
    }

    public void Notify(OdNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (_show is null)
        {
            _log.Information("Notification before the tray icon exists: {Title}", notification.Title);
            return;
        }

        _lastTarget = notification.Target;
        _lastClicked = notification.Clicked;
        _show(notification.Title, notification.Message);
    }

    /// <summary>Called by the tray icon when its balloon is clicked: opens what it names, then runs its own click.</summary>
    public void OnClicked()
    {
        Clicked?.Invoke(this, new OdNotificationClickedEventArgs(_lastTarget));
        _lastClicked?.Invoke();
    }
}
