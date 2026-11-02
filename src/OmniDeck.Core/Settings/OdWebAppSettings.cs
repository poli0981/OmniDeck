namespace OmniDeck.Core.Settings;

/// <summary>The user's choices for one web app, made in Settings → Web apps and in the app itself.</summary>
public sealed record OdWebAppSettings
{
    /// <summary>No permission answered yet.</summary>
    public static readonly IReadOnlyDictionary<string, bool> NoPermissions = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>Whether the app may show notifications; null follows the catalog's <c>notifications</c> flag.</summary>
    public bool? Notifications { get; init; }

    /// <summary>
    /// The site permissions the user answered, by WebView2 permission kind (<c>Camera</c>,
    /// <c>Microphone</c>, ...): true allowed, false blocked; a kind not listed is asked about. Compared by
    /// reference, like every collection in the settings: change it by assigning a new one.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Permissions { get; init; } = NoPermissions;

    /// <summary>Repairs values a hand-edited file may carry; the same instance when there is nothing to repair.</summary>
    public OdWebAppSettings Normalized() => NeedsRepair(Permissions)
        ? this with { Permissions = Repair(Permissions) }
        : this;

    /// <summary>Whether <paramref name="key"/> is an id or a kind as the settings keep it: not blank, no spaces around.</summary>
    internal static bool IsClean(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length == key.Trim().Length;

    private static bool NeedsRepair(IReadOnlyDictionary<string, bool>? permissions) => permissions is null || !permissions.Keys.All(IsClean);

    private static Dictionary<string, bool> Repair(IReadOnlyDictionary<string, bool>? permissions) =>
        (permissions ?? NoPermissions)
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
}
