-- The stocks module's last quote per listing, and its last price series per listing and range
-- (docs/ARCHITECTURE.md §5). The watchlist itself lives in the module's settings document.
CREATE TABLE IF NOT EXISTS stocks_quotes_cache (
    ticker     TEXT NOT NULL PRIMARY KEY,
    fetched_at TEXT NOT NULL,
    payload    TEXT NOT NULL
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS stocks_series_cache (
    series_key TEXT NOT NULL PRIMARY KEY,
    fetched_at TEXT NOT NULL,
    payload    TEXT NOT NULL
) WITHOUT ROWID;
