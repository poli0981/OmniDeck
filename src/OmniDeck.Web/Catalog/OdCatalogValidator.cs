using System.Globalization;
using System.Text.RegularExpressions;

namespace OmniDeck.Web.Catalog;

/// <summary>
/// The catalog's rules, the same ones <c>scripts/validate-catalog.ps1</c> checks in CI
/// (docs/WEBAPP-CATALOG.md §1): the app applies them again to every catalog it loads or fetches.
/// </summary>
public static partial class OdCatalogValidator
{
    /// <summary>
    /// The problems with <paramref name="catalog"/>, one line each; none when it is valid.
    /// <paramref name="today"/> bounds <c>updated</c>, with a day's slack for a PC clock running late.
    /// </summary>
    public static IReadOnlyList<string> Problems(OdCatalog catalog, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var problems = new List<string>();
        if (catalog.SchemaVersion != OdCatalog.SupportedSchemaVersion)
        {
            problems.Add($"schemaVersion: {catalog.SchemaVersion} is not {OdCatalog.SupportedSchemaVersion}");
        }

        if (!DateOnly.TryParseExact(catalog.Updated, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var updated))
        {
            problems.Add($"updated: '{catalog.Updated}' is not a yyyy-MM-dd date");
        }
        else if (updated > today.AddDays(1))
        {
            problems.Add($"updated: {catalog.Updated} is in the future");
        }

        CheckSets(catalog, problems);
        if (catalog.Apps.Count == 0)
        {
            problems.Add("apps: the catalog lists no apps");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedSets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var app in catalog.Apps)
        {
            if (!ids.Add(app.Id))
            {
                problems.Add($"apps[{app.Id}]: duplicate id");
            }

            if (!names.Add(app.Name))
            {
                problems.Add($"apps[{app.Id}]: duplicate name '{app.Name}'");
            }

            CheckApp(catalog, app, usedSets, problems);
        }

        problems.AddRange(catalog.NavAllowSets.Keys.Where(set => !usedSets.Contains(set)).Select(set => $"navAllowSets[{set}]: no app uses this set"));
        return problems;
    }

    private static void CheckSets(OdCatalog catalog, List<string> problems)
    {
        foreach (var (name, domains) in catalog.NavAllowSets)
        {
            if (!OdDomains.IsSlug(name))
            {
                problems.Add($"navAllowSets[{name}]: the name is not [a-z0-9-]");
            }

            CheckDomains($"navAllowSets[{name}]", domains, required: true, problems);
        }
    }

    private static void CheckApp(OdCatalog catalog, OdCatalogEntry app, HashSet<string> usedSets, List<string> problems)
    {
        var where = $"apps[{app.Id}]";
        if (!OdDomains.IsSlug(app.Id))
        {
            problems.Add($"{where}: the id is not [a-z0-9-]");
        }

        if (app.Name.Length is 0 or > 64)
        {
            problems.Add($"{where}: the name must have 1 to 64 characters");
        }

        if (!OdDomains.IsSlug(app.Profile))
        {
            problems.Add($"{where}: the profile is not [a-z0-9-]");
        }

        CheckDomains($"{where}.navAllow", app.NavAllow, required: true, problems);
        CheckDomains($"{where}.popupAllow", app.PopupAllow, required: false, problems);

        var allowed = new List<string>(app.NavAllow);
        if (app.NavAllowSets is { } sets)
        {
            if (sets.Count == 0 || sets.Distinct(StringComparer.Ordinal).Count() != sets.Count)
            {
                problems.Add($"{where}.navAllowSets: must list each set once, and at least one");
            }

            foreach (var set in sets)
            {
                if (catalog.NavAllowSets.TryGetValue(set, out var domains))
                {
                    allowed.AddRange(domains);
                    usedSets.Add(set);
                }
                else
                {
                    problems.Add($"{where}: navAllowSets '{set}' is not defined");
                }
            }
        }

        if (app.StartUrl.Length > 2048 || !StartUrl().IsMatch(app.StartUrl) || !Uri.TryCreate(app.StartUrl, UriKind.Absolute, out var start))
        {
            problems.Add($"{where}: startUrl is not an HTTPS URL");
        }
        else if (!OdDomains.Covers(start.Host, app.NavAllow))
        {
            problems.Add($"{where}: startUrl host '{start.Host}' is not covered by navAllow");
        }

        problems.AddRange(app.PopupAllow
            .Where(popup => !OdDomains.Covers(popup, allowed))
            .Select(popup => $"{where}: popupAllow '{popup}' is not covered by navAllow or its navAllowSets"));
    }

    private static void CheckDomains(string where, IReadOnlyList<string> domains, bool required, List<string> problems)
    {
        if (required && domains.Count == 0)
        {
            problems.Add($"{where}: lists no domains");
        }

        if (domains.Distinct(StringComparer.Ordinal).Count() != domains.Count)
        {
            problems.Add($"{where}: lists a domain twice");
        }

        problems.AddRange(domains.Where(domain => !OdDomains.IsDomain(domain)).Select(domain => $"{where}: '{domain}' is not a lowercase domain"));
    }

    [GeneratedRegex(@"^https://[^\s/?#]+(/\S*)?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StartUrl();
}
