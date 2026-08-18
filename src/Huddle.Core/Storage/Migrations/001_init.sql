CREATE TABLE IF NOT EXISTS moments (
    id           TEXT PRIMARY KEY,
    ts           TEXT NOT NULL,
    app          TEXT NOT NULL,
    window_title TEXT NOT NULL,
    summary      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_moments_ts ON moments(ts DESC);
