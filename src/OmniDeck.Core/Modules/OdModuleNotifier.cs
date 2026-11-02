namespace OmniDeck.Core.Modules;

/// <summary>A module's notifier: its notifications open the module when clicked, unless they say otherwise.</summary>
internal sealed class OdModuleNotifier(IOdNotifier notifier, string moduleId) : IOdNotifier
{
    public void Notify(OdNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        notifier.Notify(notification.Target is null ? notification with { Target = moduleId } : notification);
    }
}
