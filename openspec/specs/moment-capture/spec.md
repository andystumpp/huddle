# moment-capture Specification

## Purpose
TBD - created by archiving change add-manual-capture-and-vision. Update Purpose after archive.
## Requirements
### Requirement: Manual capture trigger

The Activity tab SHALL display a small icon button (camera glyph, 28 × 28 px) at the right edge of the "PATTERNS DETECTED N" section header. Clicking the button SHALL initiate one capture + Claude vision call. While a call is in flight, the button SHALL be disabled and visually dimmed. The button is a temporary affordance; it SHALL be removed when the scheduled tick loop is introduced in a later capability.

#### Scenario: Button is visible in the section header

- **WHEN** the Activity tab is selected
- **THEN** a camera-glyph icon button is visible at the right edge of the "PATTERNS DETECTED N" header row

#### Scenario: In-flight calls disable the button

- **WHEN** the user clicks the button and the capture + vision call is in progress
- **THEN** the button is disabled and visually dimmed until the call completes (success or failure)

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

### Requirement: Moment schema and log sink

When the API call succeeds, the system SHALL construct a moment record matching ADR 0001's schema — `id` (ULID-style string), `ts` (ISO-8601 UTC), `app`, `window_title`, `summary` — and append it as a single JSON line to `%LOCALAPPDATA%\Huddle\moments.log`. The captured frame SHALL NOT be persisted anywhere.

#### Scenario: Successful moment is appended to the log

- **WHEN** a vision call completes with a summary text
- **THEN** a single JSON line is appended to `%LOCALAPPDATA%\Huddle\moments.log` containing `id`, `ts`, `app`, `window_title`, and `summary`

#### Scenario: Failures are also logged

- **WHEN** the capture, the API call, or the response parsing fails
- **THEN** a single JSON line is appended to the same log file with an `error` field describing the failure, alongside `ts`, `app`, and `window_title` when available

#### Scenario: Frame is not persisted

- **WHEN** the capture pipeline runs end-to-end
- **THEN** the JPEG bytes are not written to any file on disk

