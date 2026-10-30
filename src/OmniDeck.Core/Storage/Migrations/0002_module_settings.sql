-- One JSON document per module: the "settings section" IOdModuleContext hands each module.
CREATE TABLE IF NOT EXISTS module_settings (
    module_id TEXT NOT NULL PRIMARY KEY,
    json      TEXT NOT NULL
) WITHOUT ROWID;
