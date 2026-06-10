# nudges Specification

## Purpose
TBD - created by archiving change add-linkedin-scenario-and-nudges. Update Purpose after archive.
## Requirements
### Requirement: Nudge storage

The app SHALL persist nudges in the existing local SQLite database at `%LOCALAPPDATA%\Huddle\huddle.db`. The `nudges` table SHALL include at minimum: `id` (TEXT primary key, ULID), `ts` (TEXT, ISO-8601 UTC timestamp of emission), `scenario` (TEXT, scenario key), `title` (TEXT), `body` (TEXT), and `sources` (TEXT, nullable, JSON array of moment IDs that justified the nudge). The table SHALL be backed by an index on `ts` descending. Every emitted nudge SHALL be inserted; storage is append-only at the API surface in this change (no dismiss / save / delete operations are exposed).

#### Scenario: Database is migrated on first run after the change ships

- **WHEN** the app launches with the existing `huddle.db` (which has only the `moments` table)
- **THEN** migration `002_nudges.sql` is applied, the `nudges` table and `idx_nudges_ts` index exist, and the `__migrations` table records `002_nudges.sql` as applied

#### Scenario: Successful nudge insert flushes to disk

- **WHEN** a scenario emits a nudge and `NudgeStore.AddAsync` is called
- **THEN** the row is inserted and the WAL is checkpointed before the call returns, so the nudge survives a subsequent force-kill

### Requirement: Scenario runs on the moment-capture tick

After each successful moment capture, the system SHALL evaluate each enabled scenario and, for each scenario whose cadence interval has elapsed since its last run, SHALL run the scenario with the most recent N moments as input (where N is the scenario's declared trail size). A scenario MAY decline to emit a nudge; declining SHALL store nothing.

#### Scenario: Scenario runs after a fresh moment lands

- **WHEN** the tick has just completed a successful capture + Claude vision call
- **THEN** any scenario that is due to run is evaluated against the recent moments (including the just-persisted one)

#### Scenario: Scenario stays silent

- **WHEN** the scenario's structured output is `{"emit": false, "reason": "..."}`
- **THEN** no row is inserted into the `nudges` table; the `reason` is captured for diagnostic display (manual-run status line)

#### Scenario: Scenario emits a nudge

- **WHEN** the scenario's structured output is `{"emit": true, "title": "...", "body": "...", "sources": [...]}`
- **THEN** a row is inserted with that `title`, `body`, and `sources` (serialized as JSON), and a new ULID `id` and current UTC `ts`

### Requirement: LinkedIn Posts scenario

The system SHALL ship with one enabled scenario in this change, identified by the key `linkedin-posts`. It SHALL run no more often than once per hour (re-evaluated in-memory per app launch), it SHALL read the 20 most recent moments as its trail, and it SHALL use the model `claude-sonnet-4-6` with structured output following the schema in `design.md` D6. The system prompt SHALL match `design.md` D5 verbatim, framing the user as a principal-level software architect drafting AI-assisted-development thought leadership.

#### Scenario: Scenario runs at startup once

- **WHEN** the app launches and the LinkedIn scenario has not yet run in this process
- **THEN** the scenario is evaluated on the next successful moment tick

#### Scenario: Scenario is throttled at one hour

- **WHEN** the scenario has run within the last hour (in-memory `s_lastRun`)
- **THEN** subsequent ticks do not run the scenario until 60 minutes have elapsed

### Requirement: Nudge card

Each nudge SHALL be rendered as a `NudgeCard` containing, top to bottom: a small scenario tag (colored dot + uppercase, letter-spaced scenario name in tertiary text color), the nudge title (semibold, primary text color, ~14.5 px), and the nudge body (regular weight, secondary text color, ~12.5 px, wrapping). The card frame matches the existing `MomentCard` style (rounded 10 px, subtle white-tint background, 1 px white-tint border). The card SHALL NOT show any action affordances (save / dismiss / share) in this change.

#### Scenario: Card shows tag, title, and body

- **WHEN** a nudge card renders
- **THEN** the scenario tag, the title, and the body are all visible

#### Scenario: Card has no action row

- **WHEN** the user hovers a nudge card
- **THEN** no buttons appear; the card has no save / dismiss / share affordances

### Requirement: Manual scenario trigger

The Nudges tab section header SHALL include a play-glyph button at its right edge that, when clicked, runs the LinkedIn scenario immediately regardless of the cadence throttle. While a manual run is in flight the button SHALL be disabled and visually dimmed, and a short inline status SHALL display next to the button ("Running…", then "Nudge emitted" / "Scenario stayed silent" / "Error: …"). A manual run still updates the scenario's `s_lastRun` — the next *scheduled* tick after a manual run is therefore subject to the normal hourly throttle.

#### Scenario: Button is visible in the section header

- **WHEN** the Nudges tab is selected
- **THEN** a play-glyph button is visible at the right edge of the "NUDGES N" header row

#### Scenario: Click runs the scenario without waiting for the throttle

- **WHEN** the user clicks the button (whether or not the scenario is due)
- **THEN** the scenario evaluates immediately against the current 20-moment trail; the button is disabled until the call returns

#### Scenario: Manual run that emits is persisted and rendered

- **WHEN** the manual run returns an emit
- **THEN** the nudge is appended to the `nudges` table, prepended to the visible list, and the status reads "Nudge emitted"

#### Scenario: Manual run that stays silent surfaces the outcome with the model's reason

- **WHEN** the manual run returns `{"emit": false, "reason": "<short justification>"}`
- **THEN** no row is inserted and the status reads `Silent: <reason>` (or `Scenario stayed silent` as a fallback when no reason is provided)

### Requirement: Nudges tab content

When the Nudges tab is selected, the content area SHALL render the most recent 20 nudges from the store as a vertically scrollable list of `NudgeCard`s, newest-first. When the store contains zero nudges (and no nudge has been emitted in the current session), the existing empty state SHALL remain visible.

#### Scenario: Empty state when no nudges exist

- **WHEN** the Nudges tab is selected and `NudgeStore.CountAsync` returns 0
- **THEN** the existing empty state (spark glyph + "No nudges right now." + watching subtitle) is visible

#### Scenario: Cards render newest-first when nudges exist

- **WHEN** the Nudges tab is selected and one or more nudges exist
- **THEN** the empty state is hidden and the cards render in `ts DESC` order, capped at the 20 most recent

#### Scenario: New nudge appears at the top in real time

- **WHEN** a scenario emits a nudge while the panel is open
- **THEN** the new card is inserted at position 0 of the visible list without restarting the app; if the empty state was visible, it is hidden

