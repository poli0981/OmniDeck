using System.Data;
using System.Globalization;
using Dapper;
using OmniDeck.Core.Storage;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// The user's events, in the <c>calendar_events</c> table. It lives as long as the app, so the Calendar
/// module and the reminders share it, and both hear about every change.
/// </summary>
public sealed class OdCalendarStore(OdDatabase database)
{
    private const string Columns = "id, uid, title, notes, starts_at, ends_at, all_day, rrule, exdates, reminder_minutes";
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss";

    /// <summary>Raised after an event is added, changed or deleted.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<OdCalendarEvent> All()
    {
        using var connection = database.Open();
        using var reader = connection.ExecuteReader($"SELECT {Columns} FROM calendar_events ORDER BY starts_at");
        var events = new List<OdCalendarEvent>();
        while (reader.Read())
        {
            events.Add(Read(reader));
        }

        return events;
    }

    public OdCalendarEvent? FindByUid(string uid)
    {
        using var connection = database.Open();
        using var reader = connection.ExecuteReader($"SELECT {Columns} FROM calendar_events WHERE uid = @uid", new { uid });
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>Adds a new event (id 0) or updates an existing one; returns it with its id.</summary>
    public OdCalendarEvent Save(OdCalendarEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var row = new
        {
            id = value.Id,
            uid = value.Uid,
            title = value.Title,
            notes = value.Notes,
            startsAt = Format(value.Start),
            endsAt = Format(value.End),
            allDay = value.AllDay,
            rrule = value.Recurrence,
            exdates = string.Join(',', value.Exceptions.Select(Format)),
            reminderMinutes = value.ReminderMinutes,
        };
        using var connection = database.Open();
        if (value.Id == 0)
        {
            var id = connection.ExecuteScalar<long>(
                "INSERT INTO calendar_events (uid, title, notes, starts_at, ends_at, all_day, rrule, exdates, reminder_minutes) "
                + "VALUES (@uid, @title, @notes, @startsAt, @endsAt, @allDay, @rrule, @exdates, @reminderMinutes) RETURNING id",
                row);
            value = value with { Id = id };
        }
        else
        {
            connection.Execute(
                "UPDATE calendar_events SET uid = @uid, title = @title, notes = @notes, starts_at = @startsAt, ends_at = @endsAt, "
                + "all_day = @allDay, rrule = @rrule, exdates = @exdates, reminder_minutes = @reminderMinutes WHERE id = @id",
                row);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return value;
    }

    public void Delete(long id)
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM calendar_events WHERE id = @id", new { id });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static OdCalendarEvent Read(IDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Uid = reader.GetString(1),
        Title = reader.GetString(2),
        Notes = reader.GetString(3),
        Start = Parse(reader.GetString(4)),
        End = Parse(reader.GetString(5)),
        AllDay = reader.GetInt64(6) != 0,
        Recurrence = reader.IsDBNull(7) ? null : reader.GetString(7),
        Exceptions = [.. reader.GetString(8).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Parse)],
        ReminderMinutes = reader.IsDBNull(9) ? null : reader.GetInt32(9),
    };

    private static string Format(DateTime value) => value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static DateTime Parse(string value) => DateTime.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture);
}
