using Microsoft.Extensions.Time.Testing;
using OmniDeck.App.Updates;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Updates;

public sealed class OdUpdateWatcherTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero));
    private readonly OdFakeUpdates _updates = new();
    private readonly OdFakeNotifier _notifier = new();

    [Fact]
    public void ChecksAfterStartup_ThenDaily_AndAnnouncesEachVersionOnce() => OdSta.Run(async () =>
    {
        using var watcher = new OdUpdateWatcher(_updates, _notifier, _time, Logger.None);
        watcher.Start();

        _time.Advance(OdUpdateWatcher.FirstCheckAfter - TimeSpan.FromSeconds(1));
        await Task.Delay(50);
        Assert.Equal(0, _updates.Checks);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(50);
        Assert.Equal(1, _updates.Checks);
        Assert.Empty(_notifier.Shown);

        _updates.Available = "0.1.0";
        _time.Advance(OdUpdateWatcher.Interval);
        await Task.Delay(50);
        _time.Advance(OdUpdateWatcher.Interval);
        await Task.Delay(50);

        Assert.Equal(3, _updates.Checks);
        var shown = Assert.Single(_notifier.Shown);
        Assert.Equal("OmniDeck 0.1.0 is available", shown.Title);
        Assert.Equal("Open Settings → About to install it.", shown.Message);
    });

    [Fact]
    public async Task AFailedCheck_StaysQuiet()
    {
        using var watcher = new OdUpdateWatcher(_updates, _notifier, _time, Logger.None);
        _updates.Failure = new HttpRequestException("offline");

        await watcher.CheckAsync();

        Assert.Equal(1, _updates.Checks);
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public void ACopyThatIsNotInstalled_NeverChecks()
    {
        _updates.Installed = false;
        using var watcher = new OdUpdateWatcher(_updates, _notifier, _time, Logger.None);

        watcher.Start();
        _time.Advance(TimeSpan.FromDays(2));

        Assert.Equal(0, _updates.Checks);
    }

    [Fact]
    public void Start_NeedsTheUiThread()
    {
        using var watcher = new OdUpdateWatcher(_updates, _notifier, _time, Logger.None);
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(watcher.Start));

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(failure);
    }
}
