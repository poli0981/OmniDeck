using System.Text.RegularExpressions;

namespace OmniDeck.Core.Reports;

/// <summary>
/// Takes what could identify the user out of text before it goes into a bug report
/// (docs/legal/PRIVACY.md §3): the user's profile folder, query strings and fragments of URLs, cookie
/// values, token-shaped values (bearer tokens, JWTs, long hex or base64 runs), email addresses, and
/// numeric ids of 15 digits or more, such as Discord's (docs/spikes/04-cdp-console.md).
/// </summary>
public static partial class OdScrubber
{
    /// <summary>What a removed value is replaced with.</summary>
    public const string Removed = "[removed]";

    public static string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scrubbed = profile.Length > 0 ? text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase) : text;
        scrubbed = UserFolder().Replace(scrubbed, "%USERPROFILE%");
        scrubbed = UrlTail().Replace(scrubbed, "${url}?" + Removed);
        scrubbed = Cookie().Replace(scrubbed, "${name}" + Removed);
        scrubbed = Bearer().Replace(scrubbed, "${scheme} " + Removed);
        scrubbed = Jwt().Replace(scrubbed, Removed);
        scrubbed = Email().Replace(scrubbed, "[email]");
        scrubbed = LongNumber().Replace(scrubbed, "[id]");
        scrubbed = LongHex().Replace(scrubbed, Removed);
        return LongBase64().Replace(scrubbed, Removed);
    }

    [GeneratedRegex(@"(?<![\w%])[a-z]:[\\/]users[\\/][^\\/\s""'<>|:*?]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserFolder();

    // A URL keeps its scheme, host and path; everything from ? or # on goes.
    [GeneratedRegex(@"(?<url>\b[a-z][a-z0-9+.-]*://[^\s?#""'<>]+)[?#][^\s""'<>]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlTail();

    [GeneratedRegex(@"(?<name>\b(?:set-)?cookie\s*[:=]\s*)[^\r\n]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Cookie();

    [GeneratedRegex(@"(?<scheme>\bbearer)\s+[a-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Bearer();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Jwt();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{15,}\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"\b[0-9a-f]{32,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongHex();

    // Session ids, keys and signatures; dots, backslashes and spaces break the run, so paths and names stay.
    [GeneratedRegex(@"(?<![A-Za-z0-9+/_-])[A-Za-z0-9+/_-]{40,}={0,2}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LongBase64();
}
