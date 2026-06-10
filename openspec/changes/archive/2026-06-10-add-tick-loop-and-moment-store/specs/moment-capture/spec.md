## REMOVED Requirements

### Requirement: Manual capture trigger

**Reason**: Replaced by the scheduled 3-minute tick. The button was designed-to-die; this change deletes it.

**Migration**: None. The capture + Claude call happens automatically on the tick.

### Requirement: Moment schema and log sink

**Reason**: Replaced by the SQLite store. The `moments.log` JSON-Lines file (and the surrounding `MomentLog` helper) are removed in favor of the canonical store at `%LOCALAPPDATA%\Huddle\huddle.db`.

**Migration**: None. Any existing `moments.log` is harmless and can be deleted by hand.

## ADDED Requirements

### Requirement: Scheduled capture tick

The capture pipeline SHALL be driven by a tick scheduler with a 180-second period. On app start, the scheduler SHALL fire one tick immediately, then continue at 180-second intervals. When the user pauses (via the existing pause button), the scheduler SHALL stop firing ticks; when the user resumes, the scheduler SHALL snap to a fresh 180-second countdown and resume firing.

#### Scenario: Tick fires immediately on app start

- **WHEN** `Huddle.exe` launches with a valid API key configured
- **THEN** the capture pipeline (capture → Claude vision call → store) runs once within a few seconds of startup

#### Scenario: Subsequent ticks fire every 180 seconds

- **WHEN** the scheduler is in the watching state
- **THEN** the capture pipeline fires 180 seconds after the previous tick completed its countdown

#### Scenario: Pause stops the tick

- **WHEN** the user clicks the pause button while watching
- **THEN** no further ticks fire until the user resumes; any in-flight capture is allowed to complete

#### Scenario: Resume restarts at a fresh 180 seconds

- **WHEN** the user clicks the play button while paused
- **THEN** the look-bar resets to 0 and the scheduler counts down a full 180 seconds before the next tick fires

### Requirement: SQLite moment store

The app SHALL persist moments in a local SQLite database at `%LOCALAPPDATA%\Huddle\huddle.db`. The store SHALL contain a single table `moments` with columns matching ADR 0001's schema — `id` (TEXT PRIMARY KEY), `ts` (TEXT, ISO-8601 UTC), `app` (TEXT), `window_title` (TEXT), `summary` (TEXT) — plus an index `idx_moments_ts` on `ts` descending. Each successful capture SHALL append one row. The captured frame SHALL NOT be persisted.

#### Scenario: Database is created on first run

- **WHEN** the app launches and `huddle.db` does not exist
- **THEN** the file is created, the `moments` table and `idx_moments_ts` index are present, and the schema matches the ADR 0001 columns

#### Scenario: A successful capture inserts a row

- **WHEN** a tick completes the capture + Claude vision call successfully
- **THEN** a single new row is inserted into `moments` with the new ULID, the UTC timestamp, the foreground app, the window title, and the summary text

#### Scenario: Frame is not persisted

- **WHEN** any tick runs end-to-end (success or failure)
- **THEN** the captured JPEG bytes are not written to any file on disk
