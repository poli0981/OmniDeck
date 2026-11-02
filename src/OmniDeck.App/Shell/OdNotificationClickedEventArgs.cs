namespace OmniDeck.App.Shell;

/// <summary>A notification was clicked; <see cref="Target"/> is what it opens, or null for just the window.</summary>
public sealed class OdNotificationClickedEventArgs(string? target) : EventArgs
{
    public string? Target { get; } = target;
}
