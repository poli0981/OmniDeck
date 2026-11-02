using System.Globalization;
using System.Text.Json;
using System.Windows.Controls;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Navigation;
using OmniDeck.Web.Runtime;
using Serilog.Core;
using Serilog.Events;

namespace OmniDeck.Tests.Web;

public sealed class OdWebRunnerTests : IDisposable
{
    private static readonly OdCatalog Catalog = OdCatalogService.Load(
        Path.Combine(OdRepository.Root, "catalog", "webapps.json"), "none.json", new DateOnly(2026, 11, 2), Logger.None);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    private readonly OdAppMeta _meta;

    public OdWebRunnerTests()
    {
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _meta = new OdAppMeta(database);
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("youtube", "https://www.youtube.com/watch?v=1", OdNavDecision.Allow)]
    [InlineData("youtube", "https://accounts.google.com.vn/accounts/SetSID", OdNavDecision.Allow)]
    [InlineData("gmail", "https://accounts.google.co.jp/accounts/SetSID", OdNavDecision.Allow)]
    [InlineData("spotify", "https://accounts.google.com.vn/accounts/SetSID", OdNavDecision.OpenExternally)]
    [InlineData("gmail", "https://evilgoogle.com/", OdNavDecision.OpenExternally)]
    [InlineData("gmail", "http://mail.google.com/", OdNavDecision.OpenExternally)]
    [InlineData("gmail", "mailto:someone@example.com", OdNavDecision.OpenExternally)]
    [InlineData("gmail", "about:blank", OdNavDecision.Allow)]
    [InlineData("spotify", "spotify:track:123", OdNavDecision.Block)]
    [InlineData("discord", "file:///C:/Windows/notepad.exe", OdNavDecision.Block)]
    public void TheMainFrame_StaysOnTheCatalogsDomains(string app, string target, OdNavDecision decision) =>
        Assert.Equal(decision, Policy(app).Navigate(new Uri(target)));

    [Theory]
    [InlineData("gmail", "https://accounts.google.com/o/oauth2/auth", OdNavDecision.Allow)]
    [InlineData("gmail", "https://mail.google.com/compose", OdNavDecision.OpenExternally)]
    [InlineData("spotify", "https://accounts.spotify.com/login", OdNavDecision.Allow)]
    [InlineData("spotify", "zoommtg://join", OdNavDecision.Block)]
    public void Popups_OpenInTheAppOnlyForSignIn(string app, string target, OdNavDecision decision) =>
        Assert.Equal(decision, Policy(app).Popup(new Uri(target)));

    [Fact]
    public void ALinkThatKeepsComingBack_IsHeldBack_UntilTheWindowPasses()
    {
        var opened = new List<Uri>();
        var links = new OdExternalLinks(_time, opened.Add);
        var heldBack = 0;
        links.HeldBack += (_, _) => heldBack++;
        var hop = new Uri("https://accounts.google.com.vn/accounts/SetSID?ssdc=1");

        var results = Enumerable.Range(0, 15).Select(i => links.Open(new Uri(hop, "?try=" + i))).ToList();

        Assert.Equal(OdExternalLinks.MostPerWindow, opened.Count);
        Assert.Equal(OdExternalLinks.MostPerWindow, results.Count(result => result));
        Assert.Equal(1, heldBack);
        Assert.True(links.Open(new Uri("https://example.com/other")));

        _time.Advance(OdExternalLinks.Window);
        Assert.True(links.Open(hop));
    }

    [Fact]
    public void ConsoleCalls_AreRenderedAsTheBrowserShowsThem()
    {
        Assert.Equal("Loaded 3 items in 12 ms", Render("""[{"type":"string","value":"Loaded %d items in %s"},{"type":"number","value":3,"description":"3"},{"type":"string","value":"12 ms"}]"""));
        Assert.Equal("styled text", Render("""[{"type":"string","value":"%cstyled text"},{"type":"string","value":"color: red"}]"""));
        Assert.Equal("100% done Object", Render("""[{"type":"string","value":"100%% done"},{"type":"object","description":"Object"}]"""));
        Assert.Equal("42 undefined NaN", Render("""[{"type":"number","value":42},{"type":"undefined"},{"type":"number","unserializableValue":"NaN"}]"""));
        Assert.Equal("", Render("[]"));
    }

    [Fact]
    public void TheConsole_KeepsTheLast200Entries_AndTheLast50Errors()
    {
        var console = new OdConsoleLog();

        for (var i = 0; i < 260; i++)
        {
            console.AddConsoleCall($$"""{"type":"{{(i % 2 == 0 ? "error" : "log")}}","args":[{"type":"string","value":"line {{i}}"}],"timestamp":1793500000000}""");
        }

        console.AddException("""{"timestamp":1793500000000,"exceptionDetails":{"text":"Uncaught","exception":{"description":"TypeError: x is not a function"}}}""");
        console.AddLogEntry("""{"entry":{"source":"network","level":"error","text":"Failed to load resource","url":"https://example.com/a.js","timestamp":1793500000000}}""");
        console.AddLogEntry("not json");

        Assert.Equal(OdConsoleLog.Capacity, console.All.Count);
        Assert.Equal(OdConsoleLog.ErrorCapacity, console.Errors.Count);
        Assert.Equal("TypeError: x is not a function", console.Errors[^2].Message);
        Assert.Equal("Failed to load resource (https://example.com/a.js)", console.Errors[^1].Message);
        Assert.Equal(OdConsoleLog.Warning, console.All[^1].Level);
        Assert.Contains("[error] TypeError: x is not a function", OdConsoleLog.Write(console.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void SignInHealth_WarnsOnlyWhenAProfileComesBackEmpty()
    {
        var log = new OdLogCapture();
        var health = new OdSignInHealth(_meta, log.Logger);
        health.AtFirstLoad("google", 5);
        health.AtClose("google", 12);

        var restarted = new OdSignInHealth(_meta, log.Logger);
        Assert.True(restarted.NeedsCheck("google"));
        restarted.AtFirstLoad("google", 0);
        Assert.False(restarted.NeedsCheck("google"));
        Assert.Equal("12", _meta.Get("web.cookies.google"));

        restarted.AtClose("google", 9);
        Assert.Equal("9", _meta.Get("web.cookies.google"));
        Assert.Equal(
            new[] { LogEventLevel.Information, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Information },
            log.Events.Select(e => e.Level));
        Assert.DoesNotContain(log.Events, e => e.RenderMessage(CultureInfo.InvariantCulture).Contains("null", StringComparison.Ordinal));
    }

    [Fact]
    public void AWebApp_ComesBackWhereItWas_AfterHibernating() => OdSta.Run(async () =>
    {
        var surfaces = new List<OdFakeSurface>();
        var health = new OdSignInHealth(_meta, Logger.None);
        var module = NewModule("gmail", health, surfaces);

        Assert.Equal("Gmail", module.Info.Title);
        Assert.Equal(OdRamClass.Heavy, module.Info.RamClass);
        Assert.Equal(new Uri("https://mail.google.com/"), module.StartPage);

        module.CreateView();
        surfaces[0].Source = new Uri("https://mail.google.com/mail/u/0/#inbox");
        await module.FirstLoadAsync();
        await module.SuspendAsync(OdSuspendReason.Hidden);
        await module.ActivateAsync();
        await module.HibernateAsync();

        Assert.Equal(new[] { "suspend", "resume", "close" }, surfaces[0].Calls, StringComparer.Ordinal);
        Assert.Equal(new Uri("https://mail.google.com/mail/u/0/#inbox"), module.StartPage);
        Assert.Equal("7", _meta.Get("web.cookies.google"));
        Assert.False(health.NeedsCheck("google"));

        module.CreateView();
        await module.CloseAsync();
        Assert.Equal(new Uri("https://mail.google.com/"), module.StartPage);
    });

    [Fact]
    public void AnAppPinnedForAudio_StaysWhilePlaying() => OdSta.Run(async () =>
    {
        var surfaces = new List<OdFakeSurface>();
        var youtube = NewModule("youtube", new OdSignInHealth(_meta, Logger.None), surfaces);
        var gmail = NewModule("gmail", new OdSignInHealth(_meta, Logger.None), surfaces);
        youtube.CreateView();
        gmail.CreateView();

        surfaces.ForEach(surface => surface.IsPlayingAudio = true);

        Assert.False(await youtube.CanSuspendAsync());
        Assert.True(await gmail.CanSuspendAsync());
        surfaces[0].IsPlayingAudio = false;
        Assert.True(await youtube.CanSuspendAsync());
    });

    private static OdNavPolicy Policy(string app) => new(Catalog.Apps.Single(entry => string.Equals(entry.Id, app, StringComparison.Ordinal)), Catalog);

    private static string Render(string arguments)
    {
        using var document = JsonDocument.Parse(arguments);
        return OdConsoleLog.Format([.. document.RootElement.EnumerateArray()]);
    }

    private OdWebAppModule NewModule(string app, OdSignInHealth health, List<OdFakeSurface> surfaces) => new(
        Catalog.Apps.Single(entry => string.Equals(entry.Id, app, StringComparison.Ordinal)),
        Catalog,
        new OdWebRuntime(new OdDataRoot(_temp.Path), new OdWebOptions(), Logger.None),
        health,
        _time,
        _ => { },
        _ =>
        {
            var surface = new OdFakeSurface();
            surfaces.Add(surface);
            return surface;
        });

    /// <summary>Stands in for the WebView2 view: records what the module asks of it.</summary>
    private sealed class OdFakeSurface : Border, IOdWebSurface
    {
        public List<string> Calls { get; } = [];

        public Uri? Source { get; set; }

        public bool IsPlayingAudio { get; set; }

        public Task SuspendAsync()
        {
            Calls.Add("suspend");
            return Task.CompletedTask;
        }

        public void Resume() => Calls.Add("resume");

        public Task<int?> CountCookiesAsync(Uri page) => Task.FromResult<int?>(7);

        public void Close() => Calls.Add("close");
    }
}
