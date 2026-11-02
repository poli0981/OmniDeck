using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Diagnostics;
using OmniDeck.Core;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.Governor;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Runtime;
using Serilog.Core;

namespace OmniDeck.Tests.Reports;

public sealed class OdBugReporterTests : IDisposable
{
    private static readonly OdCatalog Catalog = OdCatalogService.Load(
        Path.Combine(OdRepository.Root, "catalog", "webapps.json"), "none.json", new DateOnly(2026, 11, 2), Logger.None);

    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    private readonly OdDataRoot _root;
    private readonly OdSettingsService _settings;
    private readonly OdSignInHealth _health;
    private readonly OdFakeReporter _memory = new();

    public OdBugReporterTests()
    {
        _root = new OdDataRoot(_temp.Path);
        _root.EnsureCreated();
        _settings = new OdSettingsService(_root, Logger.None);
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _health = new OdSignInHealth(new OdAppMeta(database), Logger.None);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AReportAboutAWebApp_CarriesItsConsole_AndTheEndOfTheLog_Scrubbed() => OdSta.Run(async () =>
    {
        WriteLog("omnideck-20261101.log", "old", DateTime.UtcNow.AddDays(-1));
        WriteLog("omnideck-20261102.log", "new", DateTime.UtcNow);
        _health.AtClose("discord", 4);
        var (host, governor, discord) = NewHost();
        using (host)
        using (governor)
        {
            discord.Console.Add(new OdConsoleEntry(_time.GetUtcNow(), OdConsoleLog.Error, "TypeError at https://discord.com/app?token=secret"));
            await host.ShowAsync("clock");
            _memory.Modules["discord"] = 200 * OdGovernorOptions.MiB;
            await governor.TickAsync();

            var report = new OdBugReporter(_settings, host, governor, _root, Catalog, _health, _time).Compose("discord", @"Support code: 1 at C:\Users\someone\x.cs");

            Assert.Equal(OdVersionInfo.FromAssembly(typeof(OdBugReporter).Assembly).DisplayText, report.Version);
            Assert.StartsWith("Windows 11", report.Windows, StringComparison.Ordinal);
            Assert.Equal(("Standard", "Discord", "discord"), (report.Preset, report.Subject, report.ConsoleOf));
            Assert.Contains("TypeError at https://discord.com/app?[removed]", report.ConsoleLog, StringComparison.Ordinal);
            Assert.Contains("### Affected module / web app\n\nDiscord\n", report.Issue, StringComparison.Ordinal);
            Assert.Contains("TypeError at https://discord.com/app?[removed]", report.Issue, StringComparison.Ordinal);
            Assert.Contains(@"Support code: 1 at %USERPROFILE%\x.cs", report.Issue, StringComparison.Ordinal);
            Assert.Contains("| Module_clock | Active |", report.Issue, StringComparison.Ordinal);

            var lines = report.Logs.Split('\n');
            Assert.Equal(OdBugReporter.LogLines, lines.Length);
            Assert.Equal("old 100 [email]", lines[0]);
            Assert.Equal("new 299 [email]", lines[^1]);

            using var system = JsonDocument.Parse(report.SystemInfo);
            Assert.Equal("Standard", system.RootElement.GetProperty("preset").GetString());
            Assert.Equal(4, system.RootElement.GetProperty("signInCookies").GetProperty("discord").GetInt32());
            Assert.Contains(system.RootElement.GetProperty("modules").EnumerateArray(), module => string.Equals(module.GetProperty("id").GetString(), "discord", StringComparison.Ordinal));
            Assert.Equal(new OdFakeMemory().Committed + (200 * OdGovernorOptions.MiB), system.RootElement.GetProperty("memory").GetProperty("committedBytes").GetInt64());

            var url = OdBugReportFlow.IssueUrl(report).AbsoluteUri;
            Assert.StartsWith("https://github.com/poli0981/OmniDeck/issues/new?template=bug_report.yml&version=", url, StringComparison.Ordinal);
            Assert.EndsWith("&preset=Standard&module=Discord", url, StringComparison.Ordinal);
            Assert.DoesNotContain("TypeError", url, StringComparison.Ordinal);
        }
    });

    [Fact]
    public void AGeneralReport_HasNoConsole_AndListsEveryModule() => OdSta.Run(() =>
    {
        var (host, governor, _) = NewHost();
        using (host)
        using (governor)
        {
            var reporter = new OdBugReporter(_settings, host, governor, _root, Catalog, _health, _time);

            var report = reporter.Compose("", error: null);

            Assert.Equal("OmniDeck in general", report.Subject);
            Assert.Null(report.ConsoleOf);
            Assert.DoesNotContain("The error OmniDeck showed", report.Issue, StringComparison.Ordinal);
            Assert.Equal(new[] { "", "clock", "discord" }, reporter.Subjects().Select(choice => choice.Value), StringComparer.Ordinal);
            Assert.Equal("Discord", reporter.Subjects()[2].Title);
            return Task.CompletedTask;
        }
    });

    private void WriteLog(string name, string prefix, DateTime written)
    {
        var path = Path.Combine(_root.LogsDirectory, name);
        File.WriteAllLines(path, Enumerable.Range(0, 300).Select(i => $"{prefix} {i} jane@example.com"));
        File.SetLastWriteTimeUtc(path, written);
    }

    private (OdModuleHost Host, OdRamGovernor Governor, OdWebAppModule Discord) NewHost()
    {
        var discord = new OdWebAppModule(
            Catalog.Apps.Single(entry => string.Equals(entry.Id, "discord", StringComparison.Ordinal)),
            Catalog,
            new OdWebRuntime(_root, new OdWebOptions(), Logger.None),
            _health,
            new OdWebPreferences(_settings),
            _time);
        var host = new OdModuleHost([new OdFakeModule("clock"), discord], new OdDatabase(_temp.Combine("host.db"), pooling: false), Logger.None, _time, new OdFakeNotifier(), OdTestHttp.Client);
        var governor = new OdRamGovernor(host, _settings, new OdFakeMemory(), [_memory], _time, new OdFakeNotifier(), Logger.None);
        return (host, governor, discord);
    }
}
