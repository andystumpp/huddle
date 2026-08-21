# moment-capture Specification

## Purpose

Capturing a frame of what the user is doing on a scheduled tick, turning it into a short intent summary via the configured local CLI provider, and persisting only that summary. Covers capture scope (full primary display or active window), sensitive-window suppression, foreground context, the ephemeral screenshot handed to the CLI, and the SQLite moment store.

## Requirements

### Requirement: Frame capture and encoding

When the trigger fires and the foreground window is not suppressed, the system SHALL capture the configured target (the full primary display by default, or the active window when so configured — see "Capture scope is configurable"), resize the captured frame so the longest edge is at most 1280 px (preserving aspect ratio), and encode the result as a JPEG at approximately 80 % quality. The encoded frame SHALL be handed to the CLI through an ephemeral temporary file (see "The screenshot is ephemeral"); only the resulting summary text is persisted.

#### Scenario: Capture produces a JPEG under the resize cap

- **WHEN** the user clicks the trigger on a standard single-monitor setup
- **THEN** the JPEG has its longest edge ≤ 1280 px, and after the vision call only its summary text is retained

#### Scenario: Capture failure is handled

- **WHEN** the capture step throws (e.g., DRM-protected foreground content, GPU exclusivity)
- **THEN** the error is caught, no CLI call is made, and the panel header briefly shows "Error: capture failed" for ~3 s

### Requirement: Capture scope is configurable

The configuration SHALL support a capture scope of either the full primary display or the active window only, defaulting to the full primary display. When the scope is the active window, the system SHALL capture only the foreground window's own pixels (rendering the window directly rather than reading the screen region), so that windows overlapping or behind it are not captured. When the scope is the active window, the denylist check and the capture SHALL therefore concern the same single window.

#### Scenario: Active-window scope captures only the focused window

- **WHEN** the capture scope is set to the active window
- **THEN** the produced image contains only the foreground window's own content, not the full desktop and not any window overlapping it

#### Scenario: Full-screen scope is the default

- **WHEN** no capture scope is configured
- **THEN** the full primary display is captured

### Requirement: Capture is suppressed for sensitive windows

The configuration SHALL support a denylist of foreground application names and window-title substrings. When the foreground window matches the denylist at tick time, the system SHALL skip the capture entirely — no screenshot is taken and no moment is produced for that tick.

#### Scenario: A denylisted window is skipped

- **WHEN** the capture tick fires and the foreground app or window title matches a denylist entry
- **THEN** no screenshot is captured, no CLI call is made, and no moment is stored for that tick

#### Scenario: Non-matching windows capture normally

- **WHEN** the foreground window does not match any denylist entry
- **THEN** the capture proceeds as normal

### Requirement: Foreground window context

The system SHALL read the foreground window's process name (with a `.exe` suffix; for example `Code.exe`) and the foreground window's title using standard Win32 APIs. If either value cannot be obtained, the corresponding field SHALL fall back to `Unknown` (for app) or an empty string (for window title).

#### Scenario: Foreground context is captured

- **WHEN** the user has VS Code in the foreground titled "panel.jsx — huddle — Visual Studio Code" and clicks the trigger
- **THEN** the captured app is `Code.exe` and the captured window title is `panel.jsx — huddle — Visual Studio Code`

#### Scenario: Foreground process is inaccessible

- **WHEN** the foreground process can't be queried (e.g., elevation mismatch)
- **THEN** the captured app is `Unknown`, the captured window title is whatever `GetWindowText` returns (possibly empty), and the call still proceeds

### Requirement: Claude vision call

The system SHALL produce each moment summary by sending the captured screenshot and the foreground context to the **selected local CLI provider** (Claude, Copilot, or Agency), not to any API/SDK. The captured JPEG SHALL be written to a temporary file and attached to a single non-interactive CLI prompt — for the Claude provider via an `@<path>` reference in the prompt, for the Copilot/Agency provider via `--attachment <path>`. The prompt SHALL instruct the model to infer what the user is currently trying to accomplish — framing the response as **intent** ("you're trying to X", "you're verifying X") rather than **description** ("you're looking at X") — in a second-person, dry, specific, 1–2 sentence reply, hedging when the trail doesn't pin the goal down and committing when the trajectory is unambiguous. The prompt SHALL include, when present, a "Recent moments" block listing up to 6 prior moments newest-first (relative time, app, abbreviated window title, prior summary), and a `Foreground app: {app}\nWindow title: {title}` block for the current frame. When no prior moments exist, the "Recent moments" block SHALL be omitted.

The prompt SHALL additionally instruct the model to (a) **never include specific sensitive values** in the summary — no salaries, dollar amounts, account or card numbers, passwords, medical values, or personal identifiers — describing only the *kind* of thing, and (b) judge whether the frame shows sensitive personal, financial, health, credential, or PII content. The model SHALL reply with a single JSON object `{"summary": …, "sensitive": true|false}`; the system SHALL isolate that object from stdout and take the parsed `summary` as the moment summary and `sensitive` as the sensitivity flag. When the reply is not parseable JSON, the system SHALL take the whole reply as the summary and treat the frame as not sensitive.

#### Scenario: Successful call returns an intent-framed summary

- **WHEN** the CLI call completes successfully with sufficient trail
- **THEN** the parsed `summary` is treated as the moment summary and reads as an inferred goal ("you're trying to / you're verifying / you're likely working on …") rather than a screen description

#### Scenario: The screenshot is attached to the CLI

- **WHEN** the vision call runs on the Claude provider
- **THEN** the screenshot path is referenced with `@<path>` in the prompt; on the Copilot/Agency provider it is passed with `--attachment <path>`

#### Scenario: Summaries omit sensitive values

- **WHEN** the frame shows sensitive values (for example a compensation statement with salary and bonus figures)
- **THEN** the returned summary describes the kind of content ("reviewing a confidential compensation statement") and contains none of the specific values

#### Scenario: The frame is judged for sensitivity

- **WHEN** the frame shows sensitive personal, financial, health, credential, or PII content
- **THEN** the reply's `sensitive` flag is true; for ordinary non-sensitive content it is false

#### Scenario: A non-JSON reply is still used as the summary

- **WHEN** the model replies with plain text rather than the JSON object
- **THEN** the whole reply is taken as the summary and the frame is treated as not sensitive (the moment is not lost)

#### Scenario: First-ever capture has no trail

- **WHEN** the call fires with an empty moment store
- **THEN** the prompt contains only the image and the foreground block; no "Recent moments" section is rendered, and the model still produces a single-shot intent-framed summary

#### Scenario: Trail is included for subsequent captures

- **WHEN** the call fires with N existing moments in the store (1 ≤ N ≤ 6)
- **THEN** the prompt contains a "Recent moments" block with those N moments listed newest-first, ahead of the current foreground block

#### Scenario: Trail is capped at 6 moments

- **WHEN** the moment store contains more than 6 moments
- **THEN** only the 6 most recent are included in the "Recent moments" block

### Requirement: Sensitive frames are skipped by default

The configuration SHALL support a `skipSensitiveMoments` toggle that defaults to **enabled**. When a captured frame is flagged sensitive by the vision call and the toggle is enabled, the system SHALL store no moment for that tick. When the toggle is disabled, the system SHALL store the moment using the (already value-free) summary. Summaries are value-free regardless of this toggle; the toggle only decides whether a flagged frame is dropped entirely.

#### Scenario: A sensitive frame is skipped by default

- **WHEN** a capture tick's frame is flagged sensitive and `skipSensitiveMoments` is at its default
- **THEN** no moment is stored for that tick

#### Scenario: Keeping sensitive moments is opt-out

- **WHEN** `skipSensitiveMoments` is set to `false` and a frame is flagged sensitive
- **THEN** a moment is stored using the value-free summary

#### Scenario: Non-sensitive frames store normally

- **WHEN** a frame is not flagged sensitive
- **THEN** a moment is stored as normal, regardless of the toggle

### Requirement: The screenshot is ephemeral

The temporary screenshot file handed to the CLI SHALL be deleted immediately after the call returns (success or failure). Only the resulting text summary is persisted in the moment store; the raw image SHALL NOT be stored.

#### Scenario: Temp image is removed after the call

- **WHEN** a vision call completes, whether it succeeded or failed
- **THEN** the temporary screenshot file is deleted, and only the moment summary text is written to the store

### Requirement: Scheduled capture tick

The capture pipeline SHALL be driven by a tick scheduler with a 180-second period. On app start, the scheduler SHALL fire one tick immediately, then continue at 180-second intervals. When the user pauses (via the existing pause button), the scheduler SHALL stop firing ticks; when the user resumes, the scheduler SHALL snap to a fresh 180-second countdown and resume firing.

#### Scenario: Tick fires immediately on app start

- **WHEN** `Huddle.exe` launches
- **THEN** the capture pipeline (capture → CLI vision call → store) runs once within a few seconds of startup, with the configured CLI handling its own authentication

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

The app SHALL persist moments in a local SQLite database at `%LOCALAPPDATA%\Huddle\huddle.db`. The store SHALL contain a single table `moments` with columns matching ADR 0001's schema — `id` (TEXT PRIMARY KEY), `ts` (TEXT, ISO-8601 UTC), `app` (TEXT), `window_title` (TEXT), `summary` (TEXT) — plus an index `idx_moments_ts` on `ts` descending. Each successful capture SHALL append one row. The captured frame SHALL NOT be persisted in the store — it exists only as the ephemeral temporary file handed to the CLI (see "The screenshot is ephemeral"), deleted immediately after the call.

#### Scenario: Database is created on first run

- **WHEN** the app launches and `huddle.db` does not exist
- **THEN** the file is created, the `moments` table and `idx_moments_ts` index are present, and the schema matches the ADR 0001 columns

#### Scenario: A successful capture inserts a row

- **WHEN** a tick completes the capture + CLI vision call successfully
- **THEN** a single new row is inserted into `moments` with the new ULID, the UTC timestamp, the foreground app, the window title, and the summary text

#### Scenario: Frame is not persisted in the store

- **WHEN** any tick runs end-to-end (success or failure)
- **THEN** the captured JPEG is written only to the ephemeral temporary file used for the CLI call, is deleted immediately after that call, and no image bytes are stored in `moments` or otherwise retained
