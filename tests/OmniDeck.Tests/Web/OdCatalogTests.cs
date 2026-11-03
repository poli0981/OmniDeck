using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Catalog;
using Serilog.Core;

namespace OmniDeck.Tests.Web;

public sealed class OdCatalogTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    private readonly OdAppMeta _meta;

    public OdCatalogTests()
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _meta = new OdAppMeta(database);
    }

    private static string RepositoryCatalog => File.ReadAllText(Path.Combine(OdRepository.Root, "catalog", "webapps.json"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheRepositoryCatalog_PassesTheRulesCiChecks()
    {
        var catalog = OdCatalogJson.Parse(RepositoryCatalog);

        Assert.Empty(OdCatalogValidator.Problems(catalog, Today));
        Assert.Equal(OdCatalog.SupportedSchemaVersion, catalog.SchemaVersion);
        Assert.Contains("google-account-domains", Assert.Single(catalog.Apps, app => string.Equals(app.Id, "youtube", StringComparison.Ordinal)).NavAllowSets!, StringComparer.Ordinal);
        Assert.Contains("accounts.google.com.vn", catalog.NavAllowSets["google-account-domains"], StringComparer.Ordinal);
    }

    [Fact]
    public void EachBrokenRule_IsReportedOnItsOwn()
    {
        Assert.Equal("apps[youtube]: navAllowSets 'no-such-set' is not defined", Assert.Single(ProblemsAfter(root => App(root, "youtube")["navAllowSets"] = new JsonArray("no-such-set"))));
        Assert.Equal("navAllowSets[spare]: no app uses this set", Assert.Single(ProblemsAfter(root => root["navAllowSets"]!["spare"] = new JsonArray("example.com"))));
        Assert.Equal("schemaVersion: 1 is not 2", Assert.Single(ProblemsAfter(root => root["schemaVersion"] = 1)));
        Assert.Equal("updated: 2026-11-04 is in the future", Assert.Single(ProblemsAfter(root => root["updated"] = "2026-11-04")));
        Assert.Equal("apps[spotify]: startUrl is not an HTTPS URL", Assert.Single(ProblemsAfter(root => App(root, "spotify")["startUrl"] = "http://open.spotify.com/")));
        Assert.Equal("apps[spotify]: startUrl host 'example.com' is not covered by navAllow", Assert.Single(ProblemsAfter(root => App(root, "spotify")["startUrl"] = "https://example.com/")));
        Assert.Equal("apps[spotify].navAllow: 'SpotifyCdn.com' is not a lowercase domain", Assert.Single(ProblemsAfter(root => App(root, "spotify")["navAllow"]![1] = "SpotifyCdn.com")));
        Assert.Equal(
            "apps[youtube]: popupAllow 'accounts.google.com.vn' is not covered by navAllow or its navAllowSets",
            Assert.Single(ProblemsAfter(root =>
            {
                App(root, "youtube")["popupAllow"] = new JsonArray("accounts.google.com.vn");
                App(root, "youtube").Remove("navAllowSets");
            })));
        Assert.Empty(ProblemsAfter(root => App(root, "youtube")["popupAllow"] = new JsonArray("accounts.google.com.vn")));
        Assert.Equal(2, ProblemsAfter(root => root["apps"]!.AsArray().Add(App(root, "gmail").DeepClone())).Count);
    }

    [Theory]
    [InlineData("""{"surprise":true}""")]
    [InlineData("""{"apps":[]}""")]
    [InlineData("not json")]
    public void TextThatIsNotACatalog_IsAJsonException(string json) =>
        Assert.ThrowsAny<JsonException>(() => OdCatalogJson.Parse(json));

    [Fact]
    public void AnEntryWithAMissingFieldOrANullName_IsAJsonException()
    {
        var root = JsonNode.Parse(RepositoryCatalog)!.AsObject();
        App(root, "spotify").Remove("audioPin");
        Assert.ThrowsAny<JsonException>(() => OdCatalogJson.Parse(root.ToJsonString()));

        root = JsonNode.Parse(RepositoryCatalog)!.AsObject();
        App(root, "spotify")["name"] = null;
        Assert.ThrowsAny<JsonException>(() => OdCatalogJson.Parse(root.ToJsonString()));
    }

    [Fact]
    public void Domains_MatchOnWholeLabels()
    {
        Assert.True(OdDomains.Covers("mail.google.com", "google.com"));
        Assert.True(OdDomains.Covers("google.com", "google.com"));
        Assert.False(OdDomains.Covers("evilgoogle.com", "google.com"));
        Assert.False(OdDomains.Covers("google.com.evil.net", "google.com"));
    }

    [Fact]
    public void AtStartup_TheNewerOfTheBundledAndTheFetchedCatalogWins()
    {
        var bundled = _temp.Combine("bundled.json");
        var saved = Path.Combine(_temp.Path, "saved", "webapps.json");
        File.WriteAllText(bundled, RepositoryCatalog);
        var shipped = OdCatalogJson.Parse(RepositoryCatalog).Updated;
        var today = DateOnly.ParseExact(shipped, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var later = today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        Assert.Equal(shipped, OdCatalogService.Load(bundled, saved, today, Logger.None).Updated);

        Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
        File.WriteAllText(saved, Changed(root => root["updated"] = later));
        Assert.Equal(later, OdCatalogService.Load(bundled, saved, today, Logger.None).Updated);

        File.WriteAllText(saved, Changed(root => root["updated"] = "2026-10-01"));
        Assert.Equal(shipped, OdCatalogService.Load(bundled, saved, today, Logger.None).Updated);

        File.WriteAllText(saved, "{ broken");
        Assert.Equal(shipped, OdCatalogService.Load(bundled, saved, today, Logger.None).Updated);

        Assert.Empty(OdCatalogService.Load(_temp.Combine("missing.json"), _temp.Combine("missing-too.json"), today, Logger.None).Apps);
    }

    [Fact]
    public async Task ADailyCheck_KeepsAValidCatalog_AndAsksOnlyForChanges()
    {
        var saved = Path.Combine(_temp.Path, "saved", "webapps.json");
        var next = Changed(root => root["updated"] = "2026-11-03");
        var http = new OdFakeHttp(request => request.Headers.IfNoneMatch.Count > 0 ? new HttpResponseMessage(HttpStatusCode.NotModified) : Ok(next, "\"v2\""));
        using var service = NewService(saved, http);

        Assert.Equal(OdCatalogCheck.Updated, await service.CheckAsync(Token));
        Assert.Equal(next, await File.ReadAllTextAsync(saved, Token));
        Assert.Equal("\"v2\"", _meta.Get(OdAppMetaKeys.CatalogETag));
        Assert.NotNull(_meta.Get(OdAppMetaKeys.CatalogCheckedAt));
        Assert.Equal(OdCatalogService.RemoteUri, http.Requests[0].RequestUri);

        Assert.Equal(OdCatalogCheck.Unchanged, await service.CheckAsync(Token));
        Assert.Equal("\"v2\"", http.Requests[1].Headers.IfNoneMatch.Single().Tag);
    }

    [Fact]
    public async Task ABrokenOrUnreachableCatalog_IsNeverKept()
    {
        var saved = Path.Combine(_temp.Path, "saved", "webapps.json");
        var answers = new Queue<Func<HttpResponseMessage>>(
        [
            () => Ok(Changed(root => root["schemaVersion"] = 3), etag: null),
            () => Ok("{ broken", etag: null),
            () => throw new HttpRequestException("No such host is known."),
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
        ]);
        using var service = NewService(saved, new OdFakeHttp(_ => answers.Dequeue()()));

        Assert.Equal(OdCatalogCheck.Rejected, await service.CheckAsync(Token));
        Assert.Equal(OdCatalogCheck.Failed, await service.CheckAsync(Token));
        Assert.Equal(OdCatalogCheck.Failed, await service.CheckAsync(Token));
        Assert.Equal(OdCatalogCheck.Failed, await service.CheckAsync(Token));
        Assert.False(File.Exists(saved));
    }

    [Fact]
    public async Task Checks_RunAtMostOnceADay()
    {
        var http = new OdFakeHttp(_ => new HttpResponseMessage(HttpStatusCode.NotModified));
        _meta.Set(OdAppMetaKeys.CatalogCheckedAt, _time.GetUtcNow().AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        using var service = NewService(_temp.Combine("saved.json"), http);

        service.Start();
        _time.Advance(TimeSpan.FromHours(21));
        await Task.Delay(50, Token);
        Assert.Empty(http.Requests);

        _time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(50, Token);
        Assert.Single(http.Requests);

        _time.Advance(OdCatalogService.CheckEvery);
        await Task.Delay(50, Token);
        Assert.Equal(2, http.Requests.Count);
    }

    private static JsonObject App(JsonObject root, string id) =>
        root["apps"]!.AsArray().Select(app => app!.AsObject()).Single(app => string.Equals((string?)app["id"], id, StringComparison.Ordinal));

    private static string Changed(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(RepositoryCatalog)!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    private static IReadOnlyList<string> ProblemsAfter(Action<JsonObject> change) =>
        OdCatalogValidator.Problems(OdCatalogJson.Parse(Changed(change)), Today);

    private static HttpResponseMessage Ok(string json, string? etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        if (etag is not null)
        {
            response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return response;
    }

    private OdCatalogService NewService(string saved, HttpMessageHandler handler) =>
        new(new HttpClient(handler), _meta, saved, OdCatalog.Empty, _time, Logger.None);

    /// <summary>Answers requests without the network and remembers them.</summary>
    private sealed class OdFakeHttp(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }
}
