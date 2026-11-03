using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Timer;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.ModuleApps;

// Times are shown in the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdAlarmTests : IDisposable
{
    private static readonly TimeZoneInfo Pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
    private static readonly TimeOnly HalfPastSeven = new(7, 30);

    // Tuesday 3 November 2026, 07:00 in Los Angeles (PST, UTC-8).
    private static readonly DateTimeOffset TuesdaySeven = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);

    private readonly CultureInfo? _culture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly OdTempDirectory _temp = new();
    private readonly OdDatabase _database;
    private readonly FakeTimeProvider _time = new(TuesdaySeven);
    private readonly OdFakeNotifier _notifier = new();

    public OdAlarmTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        _time.SetLocalTimeZone(Pacific);
        _database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        _database.Migrate(OdCoreMigrations.All);
    }

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        _temp.Dispose();
    }

    [Theory]
    [InlineData(OdAlarmRepeat.Once, "2026-11-03 06:00", "2026-11-03 06:00", "2026-11-03 07:30")]
    [InlineData(OdAlarmRepeat.Once, "2026-11-03 08:00", "2026-11-03 08:00", "2026-11-04 07:30")]
    [InlineData(OdAlarmRepeat.Daily, "2026-11-01 00:00", "2026-11-03 07:30", "2026-11-04 07:30")]
    [InlineData(OdAlarmRepeat.Weekdays, "2026-11-01 00:00", "2026-11-06 08:00", "2026-11-09 07:30")]
    [InlineData(OdAlarmRepeat.Weekends, "2026-11-01 00:00", "2026-11-02 08:00", "2026-11-07 07:30")]
    public void Alarms_RingOnTheirDays(OdAlarmRepeat repeat, string armedAt, string after, string expected)
    {
        var alarm = new OdAlarm("a", HalfPastSeven, repeat, "", IsOn: true, Local(armedAt));

        Assert.Equal(Local(expected), OdAlarmSchedule.NextAt(alarm, Local(after), Pacific));
        Assert.Null(OdAlarmSchedule.NextAt(alarm with { IsOn = false }, Local(after), Pacific));
    }

    [Fact]
    public void DaylightSaving_NeitherSkipsNorDoublesAnAlarm()
    {
        // 14 March 2027: clocks go from 02:00 to 03:00, so 02:30 never happens.
        var gap = new OdAlarm("a", new TimeOnly(2, 30), OdAlarmRepeat.Daily, "", IsOn: true, Local("2027-03-01 00:00"));
        Assert.Equal(Local("2027-03-14 03:00"), OdAlarmSchedule.NextAt(gap, Local("2027-03-14 00:00"), Pacific));

        // 1 November 2026: 01:30 happens twice; the alarm rings the first time only.
        var twice = gap with { Time = new TimeOnly(1, 30) };
        Assert.Equal(Local("2026-11-01 01:30"), OdAlarmSchedule.NextAt(twice, Local("2026-11-01 00:00"), Pacific));
        Assert.Equal(Local("2026-11-02 01:30"), OdAlarmSchedule.NextAt(twice, Local("2026-11-01 01:30"), Pacific));
    }

    [Fact]
    public void AnAlarm_RingsAtItsTime_OpensTimer_AndAOneOffTurnsItselfOff() => OdSta.Run(async () =>
    {
        using var alarms = new OdAlarmService(_database, _time, _notifier);
        alarms.Start();
        var daily = alarms.Add(HalfPastSeven, OdAlarmRepeat.Daily, "Stand up");
        var once = alarms.Add(new TimeOnly(8, 0), OdAlarmRepeat.Once, "");

        _time.Advance(TimeSpan.FromMinutes(30));
        await Task.Delay(50);
        var rang = Assert.Single(_notifier.Raised);
        Assert.Equal("Alarm", rang.Title);
        Assert.Equal("Stand up, " + HalfPastSeven.ToString("t", CultureInfo.CurrentCulture), rang.Message);
        Assert.Equal("timer", rang.Target);

        _time.Advance(TimeSpan.FromMinutes(30));
        await Task.Delay(50);
        Assert.Equal(2, _notifier.Raised.Count);
        Assert.False(alarms.Alarms.Single(alarm => string.Equals(alarm.Id, once.Id, StringComparison.Ordinal)).IsOn);

        _time.Advance(TimeSpan.FromDays(1));
        await Task.Delay(50);
        Assert.Equal(3, _notifier.Raised.Count);

        // Kept in the database, the one-off still off.
        using var reopened = new OdAlarmService(_database, _time, _notifier);
        Assert.Equal(new[] { true, false }, reopened.Alarms.Select(alarm => alarm.IsOn));
        Assert.Equal(daily.Id, reopened.Alarms[0].Id);
    });

    [Fact]
    public void AnAlarmThePcSleptThrough_RingsOnlyWithinTheHour() => OdSta.Run(async () =>
    {
        using var alarms = new OdAlarmService(_database, _time, _notifier);
        alarms.Start();
        alarms.Add(HalfPastSeven, OdAlarmRepeat.Once, "");
        alarms.Add(new TimeOnly(9, 0), OdAlarmRepeat.Once, "");

        _time.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(15));
        await Task.Delay(50);

        Assert.Equal("9:00 AM", Assert.Single(_notifier.Raised).Message);
        Assert.All(alarms.Alarms, alarm => Assert.False(alarm.IsOn));
    });

    [Fact]
    public void SwitchedOffOrRemovedAlarms_StaySilent() => OdSta.Run(async () =>
    {
        using var alarms = new OdAlarmService(_database, _time, _notifier);
        alarms.Start();
        var off = alarms.Add(HalfPastSeven, OdAlarmRepeat.Daily, "");
        var gone = alarms.Add(HalfPastSeven, OdAlarmRepeat.Daily, "");

        alarms.SetOn(off.Id, on: false);
        alarms.Remove(gone.Id);
        _time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(50);

        Assert.Empty(_notifier.Raised);
        Assert.Single(alarms.Alarms);
        Assert.Null(alarms.NextAt(alarms.Alarms[0]));
    });

    [Fact]
    public void TheForm_AddsATypedTime_AndSaysWhenItIsNotOne() => OdSta.Run(() =>
    {
        using var service = new OdAlarmService(_database, _time, _notifier);
        using var alarms = new OdAlarmsViewModel(service);

        alarms.NewTime = "soon";
        alarms.AddCommand.Execute(null);
        Assert.Equal("That is not a time. For example " + HalfPastSeven.ToString("t", CultureInfo.CurrentCulture) + ".", alarms.Message);
        Assert.True(alarms.HasNoAlarms);

        alarms.NewTime = "7:30 AM";
        alarms.NewLabel = "Stand up";
        alarms.NewRepeat = alarms.RepeatOptions[2];
        alarms.AddCommand.Execute(null);
        var row = Assert.Single(alarms.Rows);
        Assert.Null(alarms.Message);
        Assert.Equal("", alarms.NewTime);
        Assert.Equal("Weekdays", row.Repeat);
        Assert.StartsWith("Stand up at " + HalfPastSeven.ToString("t", CultureInfo.CurrentCulture) + ", Weekdays. Next: ", row.AccessibleName, StringComparison.Ordinal);

        alarms.Rows[0].IsOn = false;
        Assert.False(service.Alarms[0].IsOn);
        Assert.Equal("Off", alarms.Rows[0].Next);

        alarms.RemoveCommand.Execute(alarms.Rows[0]);
        Assert.Empty(service.Alarms);
        return Task.CompletedTask;
    });

    private static DateTime Local(string time) => DateTime.ParseExact(time, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
