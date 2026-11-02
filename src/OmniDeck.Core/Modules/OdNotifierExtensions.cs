namespace OmniDeck.Core.Modules;

public static class OdNotifierExtensions
{
    /// <summary>A notification with a title and a message, opening nothing in particular when clicked.</summary>
    public static void Notify(this IOdNotifier notifier, string title, string message)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        notifier.Notify(new OdNotification(title, message));
    }
}
