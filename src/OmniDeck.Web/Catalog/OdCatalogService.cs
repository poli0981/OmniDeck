using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OmniDeck.Core.Storage;
using Serilog;

namespace OmniDeck.Web.Catalog;

/// <summary>
/// Which web apps this session can run (docs/WEBAPP-CATALOG.md): <see cref="LoadInstalled"/> picks the
/// newer of the catalog bundled with the build and the last one fetched from the repository, each
/// checked against the catalog's rules, before the app builds its modules. Once a day the service
/// fetches <c>main</c>'s copy (docs/legal/PRIVACY.md §2: "the daily web-app catalog refresh"); a valid
/// one is kept and applies from the next start, so the running set of modules never changes under the user.
/// </summary>
public sealed class OdCatalogService : IDisposable
{
    /// <summary>The live catalog: <c>main</c>'s copy.</summary>
    public static readonly Uri RemoteUri = new("https://raw.githubusercontent.com/poli0981/OmniDeck/main/catalog/webapps.json");

    /// <summary>How often the catalog is checked: at most once a day, as PRIVACY.md says.</summary>
    public static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);

    // The first check waits until startup is over.
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly IOdAppMeta _meta;
    private readonly string _savedPath;
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private ITimer? _timer;

    public OdCatalogService(HttpClient http, IOdAppMeta meta, OdDataRoot dataRoot, OdCatalog current, TimeProvider time, ILogger log)
        : this(http, meta, dataRoot?.CatalogFile ?? "", current, time, log)
    {
    }

    internal OdCatalogService(HttpClient http, IOdAppMeta meta, string savedPath, OdCatalog current, TimeProvider time, ILogger log)
    {
        _http = http;
        _meta = meta;
        _savedPath = savedPath;
        _time = time;
        _log = log.ForContext<OdCatalogService>();
        Current = current;
    }

    /// <summary>The catalog in effect until the next start.</summary>
    public OdCatalog Current { get; }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    /// <summary>The catalog for this session: the newer of the bundled and the last fetched copy.</summary>
    public static OdCatalog LoadInstalled(OdDataRoot dataRoot, TimeProvider time, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(time);
        return Load(Path.Combine(AppContext.BaseDirectory, "catalog", "webapps.json"), dataRoot.CatalogFile, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), log);
    }

    /// <summary>The newer (by <c>updated</c>) of two catalogs that pass the rules; one with no apps when neither does.</summary>
    internal static OdCatalog Load(string bundledPath, string savedPath, DateOnly today, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(log);
        log = log.ForContext<OdCatalogService>();
        var bundled = Read(bundledPath, "bundled", today, log);
        var saved = Read(savedPath, "fetched", today, log);
        var chosen = (bundled, saved) switch
        {
            ({ } fromBuild, { } fetched) => string.CompareOrdinal(fetched.Updated, fromBuild.Updated) > 0 ? fetched : fromBuild,
            ({ } fromBuild, null) => fromBuild,
            (null, { } fetched) => fetched,
            _ => OdCatalog.Empty,
        };
        log.Information("Web app catalog {Updated} in use ({Count} apps)", chosen.Updated, chosen.Apps.Count);
        return chosen;
    }

    /// <summary>When <c>main</c>'s copy was last checked; null before the first check.</summary>
    public DateTimeOffset? LastChecked =>
        DateTimeOffset.TryParse(_meta.Get(OdAppMetaKeys.CatalogCheckedAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var checkedAt) ? checkedAt : null;

    /// <summary>Starts the daily check: a minute after startup, or a day after the last check.</summary>
    public void Start()
    {
        var due = FirstCheckDelay;
        if (LastChecked is { } last)
        {
            var untilNext = last + CheckEvery - _time.GetUtcNow();
            due = untilNext > due ? untilNext : due;
        }

        _timer = _time.CreateTimer(OnTimer, null, due, CheckEvery);
    }

    /// <summary>Fetches <c>main</c>'s catalog unless it is unchanged, and keeps it when it is valid.</summary>
    public async Task<OdCatalogCheck> CheckAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, RemoteUri);
        if (EntityTagHeaderValue.TryParse(_meta.Get(OdAppMetaKeys.CatalogETag), out var etag))
        {
            request.Headers.IfNoneMatch.Add(etag);
        }

        // Recorded first, so a failure still waits a day before the next try.
        _meta.Set(OdAppMetaKeys.CatalogCheckedAt, _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return OdCatalogCheck.Unchanged;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var catalog = OdCatalogJson.Parse(json);
            var problems = OdCatalogValidator.Problems(catalog, Today);
            if (problems.Count > 0)
            {
                _log.Warning("The fetched web app catalog was rejected: {Problems}", problems);
                return OdCatalogCheck.Rejected;
            }

            Save(json);
            if (response.Headers.ETag is { } tag)
            {
                _meta.Set(OdAppMetaKeys.CatalogETag, tag.ToString());
            }

            _log.Information("Web app catalog {Updated} fetched; it applies from the next start", catalog.Updated);
            return OdCatalogCheck.Updated;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _log.Warning(ex, "The web app catalog could not be checked");
            return OdCatalogCheck.Failed;
        }
    }

    public void Dispose() => _timer?.Dispose();

    private void OnTimer(object? state) => _ = CheckQuietlyAsync();

    private async Task CheckQuietlyAsync()
    {
        try
        {
            await CheckAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "The web app catalog check failed");
        }
    }

    private static OdCatalog? Read(string path, string kind, DateOnly today, ILogger log)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var catalog = OdCatalogJson.Parse(File.ReadAllText(path));
            var problems = OdCatalogValidator.Problems(catalog, today);
            if (problems.Count == 0)
            {
                return catalog;
            }

            log.Warning("The {Kind} web app catalog is not valid: {Problems}", kind, problems);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            log.Warning(ex, "The {Kind} web app catalog could not be read", kind);
        }

        return null;
    }

    // Written next to its final name, then moved over it: a crash never leaves half a catalog.
    private void Save(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_savedPath)!);
        var temporary = _savedPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _savedPath, overwrite: true);
    }
}
