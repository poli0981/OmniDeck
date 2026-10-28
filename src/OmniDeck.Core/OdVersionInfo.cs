using System.Reflection;

namespace OmniDeck.Core;

/// <summary>
/// The app version shown in About and bug reports: the SemVer version plus the short commit hash,
/// both read from the informational version the SDK stamps at build time
/// (for example <c>0.0.1-alpha.1+1ab1b365867c3642a489fc43250f14d567ec1e6f</c>).
/// </summary>
public sealed record OdVersionInfo(string Version, string? Commit)
{
    private const int ShortCommitLength = 7;

    /// <summary>The version followed by the short commit, e.g. <c>0.0.1-alpha.1 (1ab1b36)</c>.</summary>
    public string DisplayText => Commit is null ? Version : $"{Version} ({Commit})";

    /// <summary>Parses an informational version of the form <c>version[+commit]</c>.</summary>
    public static OdVersionInfo Parse(string informationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(informationalVersion);

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return new OdVersionInfo(informationalVersion.Trim(), Commit: null);
        }

        var version = informationalVersion[..plus].Trim();
        var metadata = informationalVersion[(plus + 1)..].Trim();
        var commit = metadata.Length == 0 ? null : metadata[..Math.Min(ShortCommitLength, metadata.Length)];
        return new OdVersionInfo(version, commit);
    }

    /// <summary>
    /// Shown when an assembly carries no informational version, which never happens for
    /// SDK-built OmniDeck assemblies.
    /// </summary>
    public static OdVersionInfo Unknown { get; } = new("unknown", Commit: null);

    /// <summary>Reads the informational version stamped on <paramref name="assembly"/>.</summary>
    public static OdVersionInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? Unknown : Parse(informational);
    }
}
