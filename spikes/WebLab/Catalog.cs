using System.Text.Json;

namespace WebLab;

public sealed record CatalogEntry(
    string Id, string Name, string StartUrl, string Profile, string[] NavAllow, string[] PopupAllow, bool Notifications, bool AudioPin);

public static class Catalog
{
    /// <summary>Reads catalog/webapps.json from the repository this harness was built in.</summary>
    public static IReadOnlyList<CatalogEntry> Load()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "catalog", "webapps.json");
            if (File.Exists(file))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return doc.RootElement.GetProperty("apps").Deserialize<CatalogEntry[]>(options) ?? [];
            }
        }

        return [];
    }

    /// <summary>docs/WEBAPP-CATALOG.md §1: equal to an entry, or a subdomain of it on whole labels.</summary>
    public static bool Matches(string host, IEnumerable<string> domains) =>
        domains.Any(d => host.Equals(d, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    /// <summary>Scheme, host and path only: sign-in URLs carry tokens in their query strings.</summary>
    public static string Redact(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Host}{u.AbsolutePath}" : uri;
}
