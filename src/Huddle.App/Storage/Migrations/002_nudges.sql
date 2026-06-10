CREATE TABLE IF NOT EXISTS nudges (
    id        TEXT PRIMARY KEY,
    ts        TEXT NOT NULL,
    scenario  TEXT NOT NULL,
    title     TEXT NOT NULL,
    body      TEXT NOT NULL,
    sources   TEXT
);

CREATE INDEX IF NOT EXISTS idx_nudges_ts ON nudges(ts DESC);
