using System.Text.RegularExpressions;

namespace OmniDeck.Web.Catalog;

/// <summary>
/// The catalog's domain rules (docs/WEBAPP-CATALOG.md §1): lowercase domains without scheme, port,
/// path or wildcard, matched on whole DNS labels, so <c>google.com</c> covers <c>mail.google.com</c>
/// and never <c>evilgoogle.com</c>.
/// </summary>
public static partial class OdDomains
{
    /// <summary>Whether <paramref name="host"/> is <paramref name="domain"/> or one of its subdomains.</summary>
    public static bool Covers(string host, string domain)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(domain);
        return string.Equals(host, domain, StringComparison.Ordinal) || host.EndsWith("." + domain, StringComparison.Ordinal);
    }

    /// <summary>Whether any of <paramref name="domains"/> covers <paramref name="host"/>.</summary>
    public static bool Covers(string host, IEnumerable<string> domains) => domains.Any(domain => Covers(host, domain));

    /// <summary>Whether <paramref name="value"/> is a domain as the catalog writes one.</summary>
    public static bool IsDomain(string value) => value.Length <= 253 && Domain().IsMatch(value);

    /// <summary>Whether <paramref name="value"/> is a slug: an id, a profile or a set name.</summary>
    public static bool IsSlug(string value) => value.Length <= 64 && Slug().IsMatch(value);

    [GeneratedRegex(@"^([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Domain();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Slug();
}
