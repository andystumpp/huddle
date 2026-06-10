# moment-capture Specification

## Purpose
TBD - created by archiving change add-manual-capture-and-vision. Update Purpose after archive.
## Requirements
### Requirement: Primary display capture

When the trigger fires, the system SHALL capture the contents of the primary display, resize the captured frame so the longest edge is at most 1280 px (preserving aspect ratio), and encode the result as a JPEG at approximately 80 % quality. The captured frame SHALL be held in memory only — it SHALL NOT be written to disk.

#### Scenario: Capture produces a JPEG under the resize cap

- **WHEN** the user clicks the trigger on a standard single-monitor setup
- **THEN** the in-memory JPEG has its longest edge ≤ 1280 px and the raw bitmap is never written to disk

#### Scenario: Capture failure is handled

- **WHEN** the capture step throws (e.g., DRM-protected foreground content, GPU exclusivity)
- **THEN** the error is caught, no API call is made, and the panel header briefly shows "Error: capture failed" for ~3 s

### Requirement: Foreground window context

The system SHALL read the foreground window's process name (with a `.exe` suffix; for example `Code.exe`) and the foreground window's title using standard Win32 APIs. If either value cannot be obtained, the corresponding field SHALL fall back to `Unknown` (for app) or an empty string (for window title).

#### Scenario: Foreground context is captured

- **WHEN** the user has VS Code in the foreground titled "panel.jsx — huddle — Visual Studio Code" and clicks the trigger
- **THEN** the captured app is `Code.exe` and the captured window title is `panel.jsx — huddle — Visual Studio Code`

#### Scenario: Foreground process is inaccessible

- **WHEN** the foreground process can't be queried (e.g., elevation mismatch)
- **THEN** the captured app is `Unknown`, the captured window title is whatever `GetWindowText` returns (possibly empty), and the call still proceeds

### Requirement: Claude vision call

The system SHALL send the captured JPEG and the foreground context to the Claude API via the official Anthropic C# SDK, using the model `claude-sonnet-4-6` and `max_tokens` of `250`. The system prompt SHALL instruct Claude to act as "Huddle's eye" and return a single 1-2 sentence observation in second person, dry / specific voice, with no greetings and no proposed actions. The user message SHALL contain the image and a text block of the form `Foreground app: {app}\nWindow title: {title}`.

#### Scenario: Successful call returns a summary

- **WHEN** the API call completes successfully
- **THEN** the response's first text block is treated as the moment summary

#### Scenario: Model is Sonnet 4.6

- **WHEN** the API call is sent
- **THEN** the model field is `claude-sonnet-4-6` and the request's `max_tokens` is `250`

### Requirement: API key from environment

The system SHALL read the Anthropic API key from the `ANTHROPIC_API_KEY` environment variable (the SDK does this automatically). If the variable is unset or empty, the system SHALL NOT attempt the API call; instead, the panel header SHALL briefly display "Set ANTHROPIC_API_KEY" for ~3 s and an error entry SHALL be written to the log file.

#### Scenario: Missing API key

- **WHEN** the user clicks the trigger and `ANTHROPIC_API_KEY` is unset
- **THEN** no API call is attempted, the panel header shows "Set ANTHROPIC_API_KEY", and an error entry is appended to the moments log

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

