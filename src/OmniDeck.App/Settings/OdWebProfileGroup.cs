namespace OmniDeck.App.Settings;

/// <summary>The web apps that share one catalog profile, and with it their sign-in data.</summary>
/// <param name="Profile">The catalog profile.</param>
/// <param name="AppIds">The apps' ids, in catalog order.</param>
/// <param name="AppNames">The apps' names, as one list to show.</param>
public sealed record OdWebProfileGroup(string Profile, IReadOnlyList<string> AppIds, string AppNames)
{
    /// <summary>The apps' names: screen readers name a list item after its data.</summary>
    public override string ToString() => AppNames;
}
