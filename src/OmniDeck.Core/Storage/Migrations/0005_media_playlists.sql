-- The media module's queue (docs/SPEC.md §3.6): one row per named list, "queue" being the one the
-- player works through. items is a JSON array of file paths; position_ms is where the current one
-- stood when playback last stopped.
CREATE TABLE IF NOT EXISTS media_playlists (
    name        TEXT    PRIMARY KEY,
    items       TEXT    NOT NULL,
    current     INTEGER NOT NULL DEFAULT 0,
    position_ms INTEGER NOT NULL DEFAULT 0
);
