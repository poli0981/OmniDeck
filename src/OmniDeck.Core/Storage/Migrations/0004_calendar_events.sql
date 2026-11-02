-- The calendar module's events (docs/ARCHITECTURE.md §5). Times are local wall-clock times,
-- "yyyy-MM-ddTHH:mm:ss"; exdates lists the starts removed from a series, comma-separated.
CREATE TABLE IF NOT EXISTS calendar_events (
    id               INTEGER PRIMARY KEY,
    uid              TEXT    NOT NULL UNIQUE,
    title            TEXT    NOT NULL,
    notes            TEXT    NOT NULL DEFAULT '',
    starts_at        TEXT    NOT NULL,
    ends_at          TEXT    NOT NULL,
    all_day          INTEGER NOT NULL DEFAULT 0,
    rrule            TEXT,
    exdates          TEXT    NOT NULL DEFAULT '',
    reminder_minutes INTEGER
);
CREATE INDEX IF NOT EXISTS calendar_events_starts_at ON calendar_events (starts_at);
