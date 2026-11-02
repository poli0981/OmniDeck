using Microsoft.Extensions.Time.Testing;
using OmniDeck.Core.Storage;
using OmniDeck.Modules.Calendar;
using OmniDeck.Tests.Localization;
using OmniDeck.Tests.Modules;
using OmniDeck.Tests.TestSupport;

namespace OmniDeck.Tests.ModuleApps;

// Calendar texts follow the current culture, which the culture tests change.
[Collection(OdCultureCollection.Name)]
public sealed class OdCalendarTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly OdCalendarStore _store;

    public OdCalendarTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var database = new OdDatabase(_temp.Combine("omnideck.db"), pooling: false);
        database.Migrate(OdCoreMigrations.All);
        _store = new OdCalendarStore(database);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TheStore_KeepsEverythingAboutAnEvent()
    {
        var changes = 0;
        _store.Changed += (_, _) => changes++;

        var saved = _store.Save(Event("Standup", At(2026, 11, 2, 9, 30, 0)) with
        {
            Notes = "Room 4",
            Recurrence = "FREQ=WEEKLY;BYDAY=MO,WE",
            Exceptions = [At(2026, 11, 4, 9, 30, 0)],
            ReminderMinutes = 15,
        });
        var updated = _store.Save(saved with { Title = "Daily standup" });

        Assert.NotEqual(0, saved.Id);
        Assert.Equal(saved.Id, updated.Id);
        var read = Assert.Single(_store.All());
        Assert.Equal(updated with { Exceptions = read.Exceptions }, read);
        Assert.Equal(updated.Exceptions, read.Exceptions);
        Assert.Equal("Daily standup", _store.FindByUid(saved.Uid)?.Title);

        _store.Delete(saved.Id);
        Assert.Empty(_store.All());
        Assert.Equal(3, changes);
    }

    [Fact]
    public void ASeries_RepeatsWithinTheWindow_SkippingItsExceptions()
    {
        var series = Event("Review", At(2026, 11, 2, 14, 0, 0)) with
        {
            Recurrence = OdRecurrence.Rule(OdRepeat.Weekly, interval: 2, until: new DateOnly(2026, 12, 31), allDay: false),
            Exceptions = [At(2026, 11, 16, 14, 0, 0)],
        };

        var starts = OdRecurrence.Between(series, At(2026, 11, 1), At(2027, 2, 1)).Select(o => o.Start).ToList();

        Assert.Equal(
            new[] { At(2026, 11, 2, 14, 0, 0), At(2026, 11, 30, 14, 0, 0), At(2026, 12, 14, 14, 0, 0), At(2026, 12, 28, 14, 0, 0) },
            starts);
    }

    [Fact]
    public void Occurrences_ThatStartedEarlier_StillShowWhileTheyRun()
    {
        var trip = Event("Trip", At(2026, 11, 3)) with { AllDay = true, End = At(2026, 11, 6) };
        var nightly = Event("Backup", At(2026, 11, 2, 23, 0, 0)) with { End = At(2026, 11, 3, 1, 0, 0), Recurrence = "FREQ=DAILY" };

        Assert.Single(OdRecurrence.Between(trip, At(2026, 11, 5), At(2026, 11, 6)));
        Assert.Empty(OdRecurrence.Between(trip, At(2026, 11, 6), At(2026, 11, 7)));
        var night = OdRecurrence.Between(nightly, At(2026, 11, 4), At(2026, 11, 5)).Select(o => o.Start).ToList();
        Assert.Equal(new[] { At(2026, 11, 3, 23, 0, 0), At(2026, 11, 4, 23, 0, 0) }, night);
    }

    [Theory]
    [InlineData(OdRepeat.None, 1, null, false, null)]
    [InlineData(OdRepeat.Daily, 1, null, false, "FREQ=DAILY")]
    [InlineData(OdRepeat.Weekly, 2, "2026-12-31", false, "FREQ=WEEKLY;INTERVAL=2;UNTIL=20261231T235959")]
    [InlineData(OdRepeat.Yearly, 1, "2030-01-01", true, "FREQ=YEARLY;UNTIL=20300101")]
    public void TheEditorsChoices_BecomeAnRrule_AndBack(OdRepeat repeat, int interval, string? until, bool allDay, string? rule)
    {
        var last = until is null ? (DateOnly?)null : DateOnly.Parse(until, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(rule, OdRecurrence.Rule(repeat, interval, last, allDay));
        Assert.Equal((repeat, interval, last), OdRecurrence.Describe(rule));
    }

    [Fact]
    public void ARuleTheEditorCannotShow_IsKeptAsItIs()
    {
        Assert.Null(OdRecurrence.Describe("FREQ=WEEKLY;BYDAY=MO,WE"));
        Assert.Null(OdRecurrence.Describe("FREQ=MONTHLY;COUNT=3"));
    }

    [Fact]
    public void AnIcsFile_RoundTripsTheEvents()
    {
        var events = new[]
        {
            Event("Dentist", At(2026, 11, 20, 10, 30, 0)) with { Notes = "Bring the card", ReminderMinutes = 60 },
            Event("Holiday", At(2026, 12, 24)) with { AllDay = true, End = At(2026, 12, 26) },
            Event("Gym", At(2026, 11, 3, 18, 0, 0)) with { Recurrence = "FREQ=WEEKLY;BYDAY=TU,TH", Exceptions = [At(2026, 11, 5, 18, 0, 0)] },
        };

        var read = OdIcs.Read(OdIcs.Write(events));
        var none = Array.Empty<DateTime>();

        Assert.Equal(3, read.Count);
        for (var i = 0; i < events.Length; i++)
        {
            Assert.Equal(events[i] with { Exceptions = none }, read[i] with { Exceptions = none });
            Assert.Equal(events[i].Exceptions, read[i].Exceptions);
        }
    }

    [Fact]
    public void AnImportedFile_MovesFixedTimesToLocalTime_AndReadsItsReminder()
    {
        const string ics = """
            BEGIN:VCALENDAR
            VERSION:2.0
            PRODID:-//Example//EN
            BEGIN:VEVENT
            UID:abc@example.com
            DTSTART:20261105T020000Z
            DTEND:20261105T030000Z
            SUMMARY:Call
            BEGIN:VALARM
            ACTION:DISPLAY
            DESCRIPTION:Call
            TRIGGER:-PT10M
            END:VALARM
            END:VEVENT
            END:VCALENDAR
            """;

        var call = Assert.Single(OdIcs.Read(ics));

        Assert.Equal("abc@example.com", call.Uid);
        Assert.Equal(new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc).ToLocalTime(), call.Start);
        Assert.Equal(TimeSpan.FromHours(1), call.End - call.Start);
        Assert.Equal(10, call.ReminderMinutes);
        Assert.Throws<FormatException>(() => OdIcs.Read("not a calendar"));
    }

    [Fact]
    public void Reminders_FireOnTime_OpenCalendar_AndFollowChanges() => OdSta.Run(async () =>
    {
        var notifier = new OdFakeNotifier();
        using var reminders = new OdReminderService(_store, _time, notifier);
        var standup = _store.Save(Event("Standup", At(2026, 11, 2, 10, 0, 0)) with { ReminderMinutes = 15, Recurrence = "FREQ=DAILY" });
        reminders.Start();

        _time.Advance(TimeSpan.FromMinutes(44));
        await Task.Delay(50);
        Assert.Empty(notifier.Raised);

        _time.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);
        var first = Assert.Single(notifier.Raised);
        Assert.Equal("Standup", first.Title);
        Assert.Equal("calendar", first.Target);

        _store.Save(standup with { ReminderMinutes = 30 });
        _time.Advance(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(45));
        await Task.Delay(50);
        Assert.Equal(2, notifier.Raised.Count);
    });

    [Fact]
    public void AReminderDueWhileOmniDeckWasClosed_IsNotShownLater() => OdSta.Run(async () =>
    {
        var notifier = new OdFakeNotifier();
        _store.Save(Event("Earlier", At(2026, 11, 2, 9, 5, 0)) with { ReminderMinutes = 15 });
        using var reminders = new OdReminderService(_store, _time, notifier);

        reminders.Start();
        _time.Advance(TimeSpan.FromMinutes(10));
        await Task.Delay(50);

        Assert.Empty(notifier.Raised);
    });

    [Fact]
    public void NewEvents_ShowInTheMonth_AndCanBeEditedOrDeleted() => OdSta.Run(() =>
    {
        using var calendar = new OdCalendarViewModel(_store, _time);
        Assert.Equal(42, calendar.MonthDays.Count);
        var thirteenth = calendar.MonthDays.Single(day => day.Date == new DateOnly(2026, 11, 13));

        calendar.NewEventCommand.Execute(thirteenth);
        calendar.EditTitle = "Concert";
        calendar.EditStart = calendar.Times.Single(time => time.Value == new TimeOnly(20, 0));
        calendar.EditEnd = calendar.Times.Single(time => time.Value == new TimeOnly(22, 30));
        calendar.SaveCommand.Execute(null);

        var item = Assert.Single(calendar.MonthDays.Single(day => day.Date == new DateOnly(2026, 11, 13)).Items);
        Assert.Equal("Concert", item.Title);
        Assert.False(calendar.IsEditing);
        Assert.Equal(At(2026, 11, 13, 22, 30, 0), Assert.Single(_store.All()).End);

        calendar.EditCommand.Execute(item);
        Assert.True(calendar.CanDelete);
        Assert.False(calendar.CanDeleteOccurrence);
        calendar.DeleteCommand.Execute(null);

        Assert.Empty(_store.All());
        Assert.Empty(calendar.MonthDays.Single(day => day.Date == new DateOnly(2026, 11, 13)).Items);
        return Task.CompletedTask;
    });

    [Fact]
    public void DeletingOneDate_OfASeries_KeepsTheRest() => OdSta.Run(() =>
    {
        _store.Save(Event("Run", At(2026, 11, 2, 7, 0, 0)) with { Recurrence = "FREQ=DAILY" });
        using var calendar = new OdCalendarViewModel(_store, _time);
        calendar.ShowWeekCommand.Execute(null);
        Assert.Equal(7, calendar.ListDays.Count);
        Assert.All(calendar.ListDays.Where(day => day.Date >= new DateOnly(2026, 11, 2)), day => Assert.Single(day.Items));

        var wednesday = calendar.ListDays.Single(day => day.Date == new DateOnly(2026, 11, 4)).Items[0];
        calendar.EditCommand.Execute(wednesday);
        Assert.True(calendar.CanDeleteOccurrence);
        calendar.DeleteOccurrenceCommand.Execute(null);

        Assert.Empty(calendar.ListDays.Single(day => day.Date == new DateOnly(2026, 11, 4)).Items);
        Assert.Single(calendar.ListDays.Single(day => day.Date == new DateOnly(2026, 11, 5)).Items);
        Assert.Equal(new[] { At(2026, 11, 4, 7, 0, 0) }, Assert.Single(_store.All()).Exceptions);
        return Task.CompletedTask;
    });

    [Fact]
    public void Importing_AddsNewEvents_AndUpdatesKnownOnes() => OdSta.Run(() =>
    {
        using var calendar = new OdCalendarViewModel(_store, _time);
        var ics = OdIcs.Write([Event("Lunch", At(2026, 11, 3, 12, 0, 0)) with { Uid = "lunch" }]);

        Assert.Equal(1, calendar.Import(ics));
        Assert.Equal(1, calendar.Import(ics.Replace("Lunch", "Team lunch", StringComparison.Ordinal)));

        Assert.Equal("Team lunch", Assert.Single(_store.All()).Title);
        Assert.True(calendar.HasMessage);
        Assert.Throws<FormatException>(() => calendar.Import("nope"));
        return Task.CompletedTask;
    });

    [Fact]
    public void TheAgenda_ListsOnlyDaysWithEvents_AndTheEditorKeepsAnImportedRule() => OdSta.Run(() =>
    {
        _store.Save(Event("Class", At(2026, 11, 3, 8, 0, 0)) with { Recurrence = "FREQ=WEEKLY;BYDAY=TU,TH" });
        using var calendar = new OdCalendarViewModel(_store, _time);

        calendar.ShowAgendaCommand.Execute(null);
        Assert.Equal(9, calendar.ListDays.Count);
        Assert.False(calendar.IsAgendaEmpty);

        calendar.EditCommand.Execute(calendar.ListDays[0].Items[0]);
        Assert.True(calendar.HasCustomRepeat);
        calendar.EditTitle = "Class (room 2)";
        calendar.SaveCommand.Execute(null);
        Assert.Equal("FREQ=WEEKLY;BYDAY=TU,TH", Assert.Single(_store.All()).Recurrence);

        calendar.NextCommand.Execute(null);
        calendar.PreviousCommand.Execute(null);
        calendar.GoToTodayCommand.Execute(null);
        Assert.Equal(new DateOnly(2026, 11, 2), calendar.Anchor);
        return Task.CompletedTask;
    });

    private static DateTime At(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
        new(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

    private static OdCalendarEvent Event(string title, DateTime start) => new()
    {
        Uid = Guid.NewGuid().ToString("N"),
        Title = title,
        Start = start,
        End = start.AddHours(1),
    };
}
