-- The weather module's last forecast per place and units, as Open-Meteo sent it (docs/ARCHITECTURE.md §5).
CREATE TABLE IF NOT EXISTS weather_cache (
    cache_key  TEXT NOT NULL PRIMARY KEY,
    fetched_at TEXT NOT NULL,
    payload    TEXT NOT NULL
) WITHOUT ROWID;
