namespace OmniDeck.Core.Settings;

/// <summary>
/// The user's choices for the web apps, by catalog id (docs/legal/PRIVACY.md §1: per-app permissions).
/// An app without an entry shows notifications as the catalog says and asks before using a permission.
/// </summary>
public sealed record OdWebSettings
{
    /// <summary>No app with choices of its own.</summary>
    public static readonly IReadOnlyDictionary<string, OdWebAppSettings> NoApps = new Dictionary<string, OdWebAppSettings>(StringComparer.Ordinal);

    /// <summary>Each app's choices, by catalog id. Compared by reference: change it by assigning a new one.</summary>
    public IReadOnlyDictionary<string, OdWebAppSettings> Apps { get; init; } = NoApps;

    /// <summary>The choices for <paramref name="appId"/>, or the defaults.</summary>
    public OdWebAppSettings For(string appId) => Apps.TryGetValue(appId, out var app) ? app : new OdWebAppSettings();

    /// <summary>These settings with <paramref name="appId"/>'s choices changed by <paramref name="change"/>.</summary>
    public OdWebSettings With(string appId, Func<OdWebAppSettings, OdWebAppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return this with { Apps = new Dictionary<string, OdWebAppSettings>(Apps, StringComparer.Ordinal) { [appId] = change(For(appId)) } };
    }

    /// <summary>Repairs values a hand-edited file may carry; the same instance when there is nothing to repair.</summary>
    public OdWebSettings Normalized() => NeedsRepair(Apps) ? this with { Apps = Repair(Apps) } : this;

    private static bool NeedsRepair(IReadOnlyDictionary<string, OdWebAppSettings>? apps) =>
        apps is null || apps.Any(pair => !OdWebAppSettings.IsClean(pair.Key) || pair.Value is null || !ReferenceEquals(pair.Value.Normalized(), pair.Value));

    private static Dictionary<string, OdWebAppSettings> Repair(IReadOnlyDictionary<string, OdWebAppSettings>? apps) =>
        (apps ?? NoApps)
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
            .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value.Normalized(), StringComparer.Ordinal);
}
