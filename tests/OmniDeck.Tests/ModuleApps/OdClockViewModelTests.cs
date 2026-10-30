using Microsoft.Extensions.Time.Testing;
using OmniDeck.Modules.Clock;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.ModuleApps;

// Clock texts follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdClockViewModelTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 8, 30, 15, TimeSpan.Zero));
    private readonly List<OdClockSettings> _saved = [];

    public OdClockViewModelTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
    }

    [Fact]
    public void LocalTimeComesFirst_ThenTheSavedZones() => OdSta.Run(() =>
    {
        using var clock = NewClock(new OdClockSettings { Zones = ["Tokyo Standard Time", "Not/AZone", "India Standard Time"], Use24Hour = true });

        Assert.Equal(new[] { "SE Asia Standard Time", "Tokyo Standard Time", "India Standard Time" }, clock.Clocks.Select(c => c.Zone.Id), StringComparer.Ordinal);
        var (local, tokyo, india) = (clock.Clocks[0], clock.Clocks[1], clock.Clocks[2]);
        Assert.True(local.IsLocal);
        Assert.False(local.CanRemove);
        Assert.True(tokyo.CanRemove);
        Assert.Equal("Local time", local.Title);
        Assert.Equal("Local time", local.ToString());
        Assert.Equal("15:30:15", local.TimeText);
        Assert.Equal("", local.OffsetText);
        Assert.Equal("17:30:15", tokyo.TimeText);
        Assert.Equal("2 h ahead", tokyo.OffsetText);
        Assert.Equal("14:00:15", india.TimeText);
        Assert.Equal("1 h 30 min behind", india.OffsetText);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time").DisplayName, tokyo.Title);
        Assert.NotEmpty(clock.AvailableZones);
        return Task.CompletedTask;
    });

    [Fact]
    public void AddingAndRemovingZones_IsSaved() => OdSta.Run(() =>
    {
        using var clock = NewClock(new OdClockSettings());
        var paris = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

        clock.AddZoneCommand.Execute(null);
        clock.SelectedZone = paris;
        clock.AddZoneCommand.Execute(null);
        clock.AddZoneCommand.Execute(null);
        Assert.Equal(new[] { "Romance Standard Time" }, _saved[^1].Zones, StringComparer.Ordinal);
        Assert.Single(_saved);

        clock.RemoveZoneCommand.Execute(clock.Clocks[0]);
        clock.RemoveZoneCommand.Execute(clock.Clocks[1]);
        Assert.Empty(_saved[^1].Zones);
        Assert.Single(clock.Clocks);
        return Task.CompletedTask;
    });

    [Fact]
    public void SameOffsetZones_SayTheTimeIsTheSame() => OdSta.Run(() =>
    {
        using var clock = NewClock(new OdClockSettings { Zones = ["North Asia Standard Time"], Use24Hour = true });

        Assert.Equal("Same time as here", clock.Clocks[1].OffsetText);
        return Task.CompletedTask;
    });

    [Fact]
    public void TheHourFormat_SwitchesAndIsSaved() => OdSta.Run(async () =>
    {
        using var clock = NewClock(new OdClockSettings { Use24Hour = true });

        clock.Use24Hour = false;
        Assert.False(_saved[^1].Use24Hour);
        Assert.StartsWith("3:30:15", clock.Clocks[0].TimeText, StringComparison.Ordinal);

        clock.StartTicking();
        clock.StartTicking();
        _time.Advance(TimeSpan.FromSeconds(1));
        await Task.Yield();
        Assert.StartsWith("3:30:16", clock.Clocks[0].TimeText, StringComparison.Ordinal);

        clock.StopTicking();
        _time.Advance(TimeSpan.FromSeconds(5));
        await Task.Yield();
        Assert.StartsWith("3:30:16", clock.Clocks[0].TimeText, StringComparison.Ordinal);
    });

    private OdClockViewModel NewClock(OdClockSettings settings) => new(settings, _saved.Add, _time);
}
