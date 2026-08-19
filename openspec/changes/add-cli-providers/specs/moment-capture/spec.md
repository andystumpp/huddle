## MODIFIED Requirements

### Requirement: Claude vision call

The system SHALL produce each moment summary by sending the captured screenshot and the foreground context to the **selected local CLI provider** (Claude, Copilot, or Agency), not to any API/SDK. The captured JPEG SHALL be written to a temporary file and attached to a single non-interactive CLI prompt — for the Claude provider via an `@<path>` reference in the prompt, for the Copilot/Agency provider via `--attachment <path>`. The prompt SHALL instruct the model to infer what the user is currently trying to accomplish — framing the response as **intent** ("you're trying to X", "you're verifying X") rather than **description** ("you're looking at X") — in a second-person, dry, specific, 1–2 sentence reply, hedging when the trail doesn't pin the goal down and committing when the trajectory is unambiguous. The prompt SHALL include, when present, a "Recent moments" block listing up to 6 prior moments newest-first (relative time, app, abbreviated window title, prior summary), and a `Foreground app: {app}\nWindow title: {title}` block for the current frame. When no prior moments exist, the "Recent moments" block SHALL be omitted. The CLI's stdout text SHALL be taken as the moment summary.

#### Scenario: Successful call returns an intent-framed summary

- **WHEN** the CLI call completes successfully with sufficient trail
- **THEN** its stdout is treated as the moment summary and reads as an inferred goal ("you're trying to / you're verifying / you're likely working on …") rather than a screen description

#### Scenario: The screenshot is attached to the CLI

- **WHEN** the vision call runs on the Claude provider
- **THEN** the screenshot path is referenced with `@<path>` in the prompt; on the Copilot/Agency provider it is passed with `--attachment <path>`

#### Scenario: First-ever capture has no trail

- **WHEN** the call fires with an empty moment store
- **THEN** the prompt contains only the image and the foreground block; no "Recent moments" section is rendered, and the model still produces a single-shot intent-framed summary

#### Scenario: Trail is capped at 6 moments

- **WHEN** the moment store contains more than 6 moments
- **THEN** only the 6 most recent are included in the "Recent moments" block

## REMOVED Requirements

### Requirement: API key from environment

**Reason**: Vision no longer uses the Anthropic API/SDK, so no API key is read or required. The selected CLI provider authenticates through its own login (subscription or Entra-backed corporate sign-in).

**Migration**: None needed for the key itself. If the configured CLI is not signed in, the vision call fails for that tick and the panel surfaces the failure the same way other capture failures are surfaced.

## ADDED Requirements

### Requirement: The screenshot is ephemeral

The temporary screenshot file handed to the CLI SHALL be deleted immediately after the call returns (success or failure). Only the resulting text summary is persisted in the moment store; the raw image SHALL NOT be stored.

#### Scenario: Temp image is removed after the call

- **WHEN** a vision call completes, whether it succeeded or failed
- **THEN** the temporary screenshot file is deleted, and only the moment summary text is written to the store

### Requirement: Capture is suppressed for sensitive windows

The configuration SHALL support a denylist of foreground application names and window-title substrings. When the foreground window matches the denylist at tick time, the system SHALL skip the capture entirely — no screenshot is taken and no moment is produced for that tick.

#### Scenario: A denylisted window is skipped

- **WHEN** the capture tick fires and the foreground app or window title matches a denylist entry
- **THEN** no screenshot is captured, no CLI call is made, and no moment is stored for that tick

#### Scenario: Non-matching windows capture normally

- **WHEN** the foreground window does not match any denylist entry
- **THEN** the capture proceeds as normal

### Requirement: Capture scope is configurable

The configuration SHALL support a capture scope of either the full primary display or the active window only, defaulting to the full primary display. When the scope is the active window, the system SHALL capture only the foreground window's own pixels (rendering the window directly rather than reading the screen region), so that windows overlapping or behind it are not captured. When the scope is the active window, the denylist check and the capture SHALL therefore concern the same single window.

#### Scenario: Active-window scope captures only the focused window

- **WHEN** the capture scope is set to the active window
- **THEN** the produced image contains only the foreground window's own content, not the full desktop and not any window overlapping it

#### Scenario: Full-screen scope is the default

- **WHEN** no capture scope is configured
- **THEN** the full primary display is captured
