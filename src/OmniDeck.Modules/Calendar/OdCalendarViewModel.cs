using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmniDeck.Core.Localization;

namespace OmniDeck.Modules.Calendar;

/// <summary>
/// The calendar screen: a month grid, a week or an agenda list, and the editor for one event. Events
/// live in <see cref="OdCalendarStore"/>; this view model only shows them, so hibernating it loses nothing.
/// </summary>
public sealed partial class OdCalendarViewModel : ObservableObject, IDisposable
{
    private const int AgendaDays = 30;
    private const int CellItems = 3;

    private readonly OdCalendarStore _store;
    private readonly TimeProvider _time;
    private OdCalendarEvent? _editing;
    private DateTime? _editingOccurrence;
    private bool _importing;

    public OdCalendarViewModel(OdCalendarStore store, TimeProvider time)
    {
        _store = store;
        _time = time;
        _anchor = Today;
        Times = [.. Enumerable.Range(0, 96).Select(quarter => TimeChoice(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(15 * quarter))))];
        Repeats =
        [
            new(OdRepeat.None, () => OdLocalization.Get("Calendar_RepeatNone")),
            new(OdRepeat.Daily, () => OdLocalization.Get("Calendar_RepeatDaily")),
            new(OdRepeat.Weekly, () => OdLocalization.Get("Calendar_RepeatWeekly")),
            new(OdRepeat.Monthly, () => OdLocalization.Get("Calendar_RepeatMonthly")),
            new(OdRepeat.Yearly, () => OdLocalization.Get("Calendar_RepeatYearly")),
        ];
        Intervals = [.. Enumerable.Range(1, 12).Select(interval => new OdCalendarChoice<int>(interval, () => EveryLabel(interval)))];
        Reminders =
        [
            new(null, () => OdLocalization.Get("Calendar_ReminderNone")),
            new(0, () => OdLocalization.Get("Calendar_ReminderAtStart")),
            new(5, () => OdLocalization.Format("Calendar_ReminderMinutes", 5)),
            new(15, () => OdLocalization.Format("Calendar_ReminderMinutes", 15)),
            new(30, () => OdLocalization.Format("Calendar_ReminderMinutes", 30)),
            new(60, () => OdLocalization.Get("Calendar_ReminderHour")),
            new(1440, () => OdLocalization.Get("Calendar_ReminderDay")),
        ];
        WeekdayNames = [.. Enumerable.Range(0, 7).Select(day => Culture.DateTimeFormat.AbbreviatedDayNames[((int)FirstDay + day) % 7])];
        _store.Changed += OnStoreChanged;
        Refresh();
        OdLocalization.FollowCulture(OnCultureChanged);
    }

    public ObservableCollection<OdCalendarDay> MonthDays { get; } = [];

    /// <summary>The week's days, or the agenda's days that have events.</summary>
    public ObservableCollection<OdCalendarDay> ListDays { get; } = [];

    public IReadOnlyList<string> WeekdayNames { get; }

    public IReadOnlyList<OdCalendarChoice<TimeOnly>> Times { get; }

    public IReadOnlyList<OdCalendarChoice<OdRepeat>> Repeats { get; }

    /// <summary>"every 2 weeks": each interval as a phrase in the repeat's unit, which follows the repeat chosen.</summary>
    public IReadOnlyList<OdCalendarChoice<int>> Intervals { get; }

    public IReadOnlyList<OdCalendarChoice<int?>> Reminders { get; }

    public bool IsMonth => Mode == OdCalendarMode.Month;

    public bool IsList => Mode != OdCalendarMode.Month;

    public bool IsWeek => Mode == OdCalendarMode.Week;

    public bool IsAgenda => Mode == OdCalendarMode.Agenda;

    public bool IsAgendaEmpty => Mode == OdCalendarMode.Agenda && ListDays.Count == 0;

    public bool CanDelete => _editing is not null;

    public bool CanDeleteOccurrence => _editing?.Recurrence is not null && _editingOccurrence is not null;

    public bool HasCustomRepeat => EditCustomRule is not null;

    public bool HasTimes => !EditAllDay;

    public bool IsRepeating => EditRepeat is { Value: not OdRepeat.None };

    public bool HasMessage => Message is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonth), nameof(IsList), nameof(IsWeek), nameof(IsAgenda), nameof(IsAgendaEmpty))]
    private OdCalendarMode _mode = OdCalendarMode.Month;

    [ObservableProperty]
    private DateOnly _anchor;

    [ObservableProperty]
    private string _periodText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editTitle = "";

    [ObservableProperty]
    private DateTime? _editDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTimes))]
    private bool _editAllDay;

    [ObservableProperty]
    private OdCalendarChoice<TimeOnly>? _editStart;

    [ObservableProperty]
    private OdCalendarChoice<TimeOnly>? _editEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepeating))]
    private OdCalendarChoice<OdRepeat>? _editRepeat;

    [ObservableProperty]
    private int _editInterval = 1;

    [ObservableProperty]
    private DateTime? _editUntil;

    [ObservableProperty]
    private OdCalendarChoice<int?>? _editReminder;

    [ObservableProperty]
    private string _editNotes = "";

    /// <summary>An imported rule the editor's choices cannot show; kept unless the user picks a repeat.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomRepeat))]
    private string? _editCustomRule;

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    private static DayOfWeek FirstDay => Culture.DateTimeFormat.FirstDayOfWeek;

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    /// <summary>Re-reads the events, as when the module is shown again on another day.</summary>
    public void Refresh()
    {
        var events = _store.All();
        switch (Mode)
        {
            case OdCalendarMode.Month:
                BuildMonth(events);
                break;
            case OdCalendarMode.Week:
                BuildWeek(events);
                break;
            default:
                BuildAgenda(events);
                break;
        }

        OnPropertyChanged(nameof(IsAgendaEmpty));
    }

    /// <summary>Adds or updates the events in an iCalendar file; returns how many it held.</summary>
    /// <exception cref="FormatException">The text is not an iCalendar file.</exception>
    public int Import(string ics)
    {
        var events = OdIcs.Read(ics);
        _importing = true;
        try
        {
            foreach (var value in events)
            {
                _store.Save(_store.FindByUid(value.Uid) is { } existing ? value with { Id = existing.Id } : value);
            }
        }
        finally
        {
            _importing = false;
        }

        Refresh();
        Message = OdLocalization.Format("Calendar_Imported", events.Count);
        return events.Count;
    }

    /// <summary>Every event as an iCalendar file.</summary>
    public string Export() => OdIcs.Write(_store.All());

    /// <summary>Reports a file that could not be read or written.</summary>
    public void Report(string message) => Message = message;

    public void Dispose() => _store.Changed -= OnStoreChanged;

    [RelayCommand]
    private void ShowMonth() => Mode = OdCalendarMode.Month;

    [RelayCommand]
    private void ShowWeek() => Mode = OdCalendarMode.Week;

    [RelayCommand]
    private void ShowAgenda()
    {
        Anchor = Today;
        Mode = OdCalendarMode.Agenda;
    }

    [RelayCommand]
    private void Previous() => Anchor = Mode switch
    {
        OdCalendarMode.Month => Anchor.AddMonths(-1),
        OdCalendarMode.Week => Anchor.AddDays(-7),
        _ => Anchor.AddDays(-AgendaDays),
    };

    [RelayCommand]
    private void Next() => Anchor = Mode switch
    {
        OdCalendarMode.Month => Anchor.AddMonths(1),
        OdCalendarMode.Week => Anchor.AddDays(7),
        _ => Anchor.AddDays(AgendaDays),
    };

    [RelayCommand]
    private void GoToToday() => Anchor = Today;

    [RelayCommand]
    private void NewEvent(OdCalendarDay? day)
    {
        _editing = null;
        _editingOccurrence = null;
        var date = day?.Date ?? (Mode == OdCalendarMode.Month && Anchor.Month != Today.Month ? Anchor : Today);
        OpenEditor(new OdCalendarEvent
        {
            Uid = Guid.NewGuid().ToString("N"),
            Title = "",
            Start = date.ToDateTime(new TimeOnly(9, 0)),
            End = date.ToDateTime(new TimeOnly(10, 0)),
        });
    }

    [RelayCommand]
    private void Edit(OdCalendarItem item)
    {
        _editing = item.Occurrence.Event;
        _editingOccurrence = item.Occurrence.Start;
        OpenEditor(item.Occurrence.Event);
    }

    [RelayCommand]
    private void Save()
    {
        if (EditDate is not { } date)
        {
            Message = OdLocalization.Get("Calendar_NeedDate");
            return;
        }

        var day = DateOnly.FromDateTime(date);
        var (start, end) = Span(day);
        var rule = EditCustomRule ?? OdRecurrence.Rule(
            EditRepeat?.Value ?? OdRepeat.None,
            EditInterval,
            EditUntil is { } until ? DateOnly.FromDateTime(until) : null,
            EditAllDay);
        var value = (_editing ?? new OdCalendarEvent { Uid = Guid.NewGuid().ToString("N"), Title = "" }) with
        {
            Title = EditTitle.Trim(),
            Notes = EditNotes,
            Start = start,
            End = end,
            AllDay = EditAllDay,
            Recurrence = rule,
            Exceptions = rule is null ? [] : _editing?.Exceptions ?? [],
            ReminderMinutes = EditReminder?.Value,
        };
        CloseEditor();
        _store.Save(value);
        Anchor = Mode == OdCalendarMode.Agenda ? Anchor : day;
    }

    [RelayCommand]
    private void Cancel() => CloseEditor();

    [RelayCommand]
    private void Delete()
    {
        if (_editing is { } value)
        {
            CloseEditor();
            _store.Delete(value.Id);
        }
    }

    [RelayCommand]
    private void DeleteOccurrence()
    {
        if (_editing is { } value && _editingOccurrence is { } occurrence)
        {
            CloseEditor();
            _store.Save(value with { Exceptions = [.. value.Exceptions, occurrence] });
        }
    }

    partial void OnModeChanged(OdCalendarMode value) => Refresh();

    partial void OnAnchorChanged(DateOnly value) => Refresh();

    partial void OnEditRepeatChanged(OdCalendarChoice<OdRepeat>? value)
    {
        // Picking a repeat replaces an imported rule the choices could not show.
        if (value is not null)
        {
            EditCustomRule = null;
        }

        // The intervals say the repeat's unit: every 2 weeks, every 2 months.
        foreach (var interval in Intervals)
        {
            interval.RefreshLabel();
        }
    }

    // A change of language: the editor's lists in place, so their selections stay, and the days again.
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        foreach (var choice in Repeats.Concat<OdLocalizedOption>(Reminders).Concat(Intervals))
        {
            choice.RefreshLabel();
        }

        Refresh();
    }

    /// <summary>The interval as one phrase in the repeat's unit, worded as each language words it.</summary>
    private string EveryLabel(int interval) => (EditRepeat?.Value ?? OdRepeat.Weekly, interval) switch
    {
        (OdRepeat.Daily, 1) => OdLocalization.Get("Calendar_EveryDay"),
        (OdRepeat.Daily, _) => OdLocalization.Format("Calendar_EveryDays", interval),
        (OdRepeat.Monthly, 1) => OdLocalization.Get("Calendar_EveryMonth"),
        (OdRepeat.Monthly, _) => OdLocalization.Format("Calendar_EveryMonths", interval),
        (OdRepeat.Yearly, 1) => OdLocalization.Get("Calendar_EveryYear"),
        (OdRepeat.Yearly, _) => OdLocalization.Format("Calendar_EveryYears", interval),
        (_, 1) => OdLocalization.Get("Calendar_EveryWeek"),
        _ => OdLocalization.Format("Calendar_EveryWeeks", interval),
    };

    private static OdCalendarChoice<TimeOnly> TimeChoice(TimeOnly time) => new(time, () => time.ToString("t", Culture));

    private static string Title(OdCalendarEvent value) => value.Title.Length > 0 ? value.Title : OdLocalization.Get("Calendar_Untitled");

    private static OdCalendarItem Item(OdOccurrence occurrence) => new(
        occurrence.Event.AllDay ? OdLocalization.Get("Calendar_AllDay") : occurrence.Start.ToString("t", Culture),
        Title(occurrence.Event),
        occurrence);

    private static List<OdOccurrence> Occurrences(IReadOnlyList<OdCalendarEvent> events, DateTime from, DateTime to) =>
        [.. events.SelectMany(value => OdRecurrence.Between(value, from, to)).OrderBy(occurrence => !occurrence.Event.AllDay).ThenBy(occurrence => occurrence.Start)];

    private static List<OdOccurrence> On(List<OdOccurrence> occurrences, DateOnly date)
    {
        var from = date.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);

        // An event with no length still belongs to the day it starts on.
        return [.. occurrences.Where(occurrence => occurrence.Start < to && (occurrence.End > from || occurrence.Start >= from))];
    }

    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-((7 + (int)date.DayOfWeek - (int)FirstDay) % 7));

    private void BuildMonth(IReadOnlyList<OdCalendarEvent> events)
    {
        var first = new DateOnly(Anchor.Year, Anchor.Month, 1);
        var start = WeekStart(first);
        var occurrences = Occurrences(events, start.ToDateTime(TimeOnly.MinValue), start.AddDays(42).ToDateTime(TimeOnly.MinValue));
        MonthDays.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var items = On(occurrences, date);
            MonthDays.Add(new OdCalendarDay(
                date,
                date.Day.ToString(Culture),
                date.Month == Anchor.Month,
                date == Today,
                [.. items.Take(CellItems).Select(Item)],
                items.Count > CellItems ? OdLocalization.Format("Calendar_More", items.Count - CellItems) : null));
        }

        PeriodText = first.ToString("Y", Culture);
    }

    private void BuildWeek(IReadOnlyList<OdCalendarEvent> events)
    {
        var start = WeekStart(Anchor);
        var occurrences = Occurrences(events, start.ToDateTime(TimeOnly.MinValue), start.AddDays(7).ToDateTime(TimeOnly.MinValue));
        ListDays.Clear();
        for (var i = 0; i < 7; i++)
        {
            ListDays.Add(ListDay(start.AddDays(i), On(occurrences, start.AddDays(i))));
        }

        PeriodText = OdLocalization.Format("Calendar_Range", start.ToString("d", Culture), start.AddDays(6).ToString("d", Culture));
    }

    private void BuildAgenda(IReadOnlyList<OdCalendarEvent> events)
    {
        var occurrences = Occurrences(events, Anchor.ToDateTime(TimeOnly.MinValue), Anchor.AddDays(AgendaDays).ToDateTime(TimeOnly.MinValue));
        ListDays.Clear();
        for (var i = 0; i < AgendaDays; i++)
        {
            var items = On(occurrences, Anchor.AddDays(i));
            if (items.Count > 0)
            {
                ListDays.Add(ListDay(Anchor.AddDays(i), items));
            }
        }

        PeriodText = OdLocalization.Format("Calendar_Range", Anchor.ToString("d", Culture), Anchor.AddDays(AgendaDays - 1).ToString("d", Culture));
    }

    private OdCalendarDay ListDay(DateOnly date, List<OdOccurrence> items) =>
        new(date, date.ToString("D", Culture), IsInPeriod: true, date == Today, [.. items.Select(Item)], More: null);

    private (DateTime Start, DateTime End) Span(DateOnly day)
    {
        if (EditAllDay)
        {
            var morning = day.ToDateTime(TimeOnly.MinValue);
            return (morning, morning.AddDays(1));
        }

        var start = day.ToDateTime(EditStart?.Value ?? new TimeOnly(9, 0));
        var end = day.ToDateTime(EditEnd?.Value ?? EditStart?.Value.AddHours(1) ?? new TimeOnly(10, 0));

        // An end at or before the start runs past midnight.
        return (start, end > start ? end : end.AddDays(1));
    }

    private void OpenEditor(OdCalendarEvent value)
    {
        EditTitle = value.Title;
        EditNotes = value.Notes;
        EditDate = value.Start.Date;
        EditAllDay = value.AllDay;
        EditStart = Times.FirstOrDefault(choice => choice.Value == TimeOnly.FromDateTime(value.Start)) ?? TimeChoice(TimeOnly.FromDateTime(value.Start));
        EditEnd = Times.FirstOrDefault(choice => choice.Value == TimeOnly.FromDateTime(value.End)) ?? TimeChoice(TimeOnly.FromDateTime(value.End));
        EditReminder = Reminders.FirstOrDefault(choice => choice.Value == value.ReminderMinutes) ?? Reminders[0];
        if (OdRecurrence.Describe(value.Recurrence) is { } simple)
        {
            EditRepeat = Repeats.First(choice => choice.Value == simple.Repeat);
            EditInterval = Math.Clamp(simple.Interval, 1, 12);
            EditUntil = simple.Until?.ToDateTime(TimeOnly.MinValue);
            EditCustomRule = null;
        }
        else
        {
            EditRepeat = null;
            EditInterval = 1;
            EditUntil = null;
            EditCustomRule = value.Recurrence;
        }

        Message = null;
        IsEditing = true;
        NotifyEditorCommands();
    }

    private void CloseEditor()
    {
        IsEditing = false;
        _editing = null;
        _editingOccurrence = null;
        NotifyEditorCommands();
    }

    private void NotifyEditorCommands()
    {
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanDeleteOccurrence));
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (!_importing)
        {
            Refresh();
        }
    }
}
