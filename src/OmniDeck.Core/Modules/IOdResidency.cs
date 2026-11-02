namespace OmniDeck.Core.Modules;

/// <summary>
/// A module that keeps working while hidden, such as a web app that shows notifications: the governor
/// never demotes it for being hidden, only to stay under the memory caps, and says so when it does
/// (docs/RAM-GOVERNOR.md §3).
/// </summary>
public interface IOdResidency
{
    /// <summary>Whether the module needs to stay live while hidden, as things stand now.</summary>
    bool StaysResident { get; }
}
