namespace OmniDeck.Core.Settings;

/// <summary>
/// Which releases the update check offers (RELEASING.md): with pre-releases (alphas and betas), or only
/// stable ones. Without a choice of the user's it follows the build that runs: a pre-release build keeps
/// taking pre-releases, as every version below 1.0.0 is one, and a stable build takes stable ones only.
/// </summary>
public static class OdUpdateChannel
{
    /// <summary>Whether updates include pre-releases, given the user's <paramref name="choice"/> and the <paramref name="runningVersion"/>.</summary>
    public static bool IncludesPrereleases(bool? choice, string runningVersion) => choice ?? IsPrerelease(runningVersion);

    /// <summary>A version with a suffix (0.3.0-alpha.1, 1.1.0-beta.2) or below 1.0.0 is a pre-release; "unknown" is taken as one.</summary>
    public static bool IsPrerelease(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var core = version.Split('+')[0];
        if (core.Contains('-', StringComparison.Ordinal))
        {
            return true;
        }

        return !Version.TryParse(core, out var parsed) || parsed.Major < 1;
    }
}
