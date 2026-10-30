using OmniDeck.Core.Modules;

namespace OmniDeck.Tests.Modules;

/// <summary>Collects notifications instead of showing them.</summary>
public sealed class OdFakeNotifier : IOdNotifier
{
    public IList<(string Title, string Message)> Shown { get; } = [];

    public void Notify(string title, string message) => Shown.Add((title, message));
}
