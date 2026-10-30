-- App-wide key/value facts: the schema version and the legal acknowledgment (docs/ARCHITECTURE.md §5).
CREATE TABLE IF NOT EXISTS app_meta (
    key   TEXT NOT NULL PRIMARY KEY,
    value TEXT NOT NULL
) WITHOUT ROWID;
