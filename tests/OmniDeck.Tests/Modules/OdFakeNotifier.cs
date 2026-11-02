using OmniDeck.Core.Modules;

namespace OmniDeck.Tests.Modules;

/// <summary>Collects notifications instead of showing them.</summary>
public sealed class OdFakeNotifier : IOdNotifier
{
    /// <summary>Every notification, as raised.</summary>
    public IList<OdNotification> Raised { get; } = [];

    /// <summary>The title and message of each notification.</summary>
    public IList<(string Title, string Message)> Shown => [.. Raised.Select(n => (n.Title, n.Message))];

    public void Notify(OdNotification notification) => Raised.Add(notification);
}
