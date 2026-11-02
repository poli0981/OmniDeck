using System.IO.Compression;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Reports;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.Reports;

public sealed class OdBugReportsTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero));
    private readonly OdBugReports _reports;

    public OdBugReportsTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _reports = new OdBugReports(new OdDataRoot(_temp.Path), _time);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AReport_IsAZipOfItsFiles()
    {
        var path = _reports.Save(Report() with { ConsoleLog = "[error] boom", ConsoleOf = "discord" });

        Assert.Equal(Path.Combine(_temp.Path, "reports", "bugreport-20261102-030000.zip"), path);
        using var zip = ZipFile.OpenRead(path);
        Assert.Equal(new[] { "issue.md", "system.json", "logs.txt", "console-discord.txt" }, zip.Entries.Select(entry => entry.FullName), StringComparer.Ordinal);
        using var issue = new StreamReader(zip.GetEntry("issue.md")!.Open());
        Assert.Equal("### OmniDeck version", issue.ReadToEnd());
    }

    [Fact]
    public void WithoutAWebApp_ThereIsNoConsoleFile()
    {
        using var zip = ZipFile.OpenRead(_reports.Save(Report()));

        Assert.Equal(3, zip.Entries.Count);
    }

    [Fact]
    public void TheFolder_KeepsTheTenNewest_AndTwoInOneSecondBothFit()
    {
        var first = _reports.Save(Report());
        var twin = _reports.Save(Report());
        for (var i = 0; i < OdBugReports.Kept; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            _reports.Save(Report());
        }

        Assert.EndsWith("bugreport-20261102-030000-2.zip", twin, StringComparison.Ordinal);
        var kept = Directory.GetFiles(Path.Combine(_temp.Path, "reports"));
        Assert.Equal(OdBugReports.Kept, kept.Length);
        Assert.DoesNotContain(first, kept, StringComparer.Ordinal);
        Assert.DoesNotContain(twin, kept, StringComparer.Ordinal);
    }

    private static OdBugReport Report() => new()
    {
        Version = "0.2.0-alpha.1 (a0b4734)",
        Windows = "Windows 11 24H2 (26100.1)",
        Preset = "Standard",
        Subject = "Discord",
        Issue = "### OmniDeck version",
        SystemInfo = "{}",
        Logs = "line",
    };
}
