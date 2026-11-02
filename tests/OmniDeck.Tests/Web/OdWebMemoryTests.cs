using OmniDeck.Core.Governor;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web.Runtime;
using Serilog.Core;

namespace OmniDeck.Tests.Web;

public sealed class OdWebMemoryTests
{
    [Fact]
    public void Processes_CountForTheAppsWhosePagesTheyHost_TheRestIsShared()
    {
        OdWebPage[] pages =
        [
            new("gmail", 11, "mail.google.com"),
            new("youtube", 22, "www.youtube.com"),
            new("calendar", 0, "calendar.google.com"),
        ];
        OdWebProcess[] processes =
        [
            new(1, []),
            new(2, [new OdWebFrame(11, "mail.google.com")]),
            new(3, [new OdWebFrame(22, "www.youtube.com"), new OdWebFrame(11, "mail.google.com")]),
            new(4, [new OdWebFrame(99, "calendar.google.com")]),
            new(5, [new OdWebFrame(98, "accounts.google.com")]),
        ];
        var sizes = new Dictionary<int, long> { [1] = 100, [2] = 300, [3] = 201, [4] = 50, [5] = 40 };

        var report = OdWebMemoryReporter.Attribute(processes, pages, id => sizes[id]);

        Assert.Equal(300 + 100, report.Modules["gmail"]);
        Assert.Equal(100, report.Modules["youtube"]);
        Assert.Equal(50, report.Modules["calendar"]);
        Assert.Equal(100 + 1 + 40, report.SharedBytes);
        Assert.Equal(sizes.Values.Sum(), report.SharedBytes + report.Modules.Values.Sum());
    }

    [Fact]
    public async Task BeforeAnyWebAppOpens_ThereIsNothingToReport()
    {
        using var temp = new OdTempDirectory();
        var runtime = new OdWebRuntime(new OdDataRoot(temp.Path), new OdWebOptions(), Logger.None);

        var report = await new OdWebMemoryReporter(runtime, Logger.None).ReportAsync();

        Assert.Same(OdMemoryReport.None, report);
        Assert.Null(runtime.Current);
    }

    [Fact]
    public void AHostTwoAppsShow_IsLeftShared_AndOpenAppsStillGetAFigure()
    {
        OdWebPage[] pages = [new("a", 0, "example.com"), new("b", 0, "example.com")];
        OdWebProcess[] processes = [new(7, [new OdWebFrame(5, "example.com")])];

        var report = OdWebMemoryReporter.Attribute(processes, pages, _ => 80);

        Assert.Equal(80, report.SharedBytes);
        Assert.Equal(0, report.Modules["a"]);
        Assert.Equal(0, report.Modules["b"]);
    }
}
