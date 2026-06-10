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

After each successful moment capture, the system SHALL evaluate **each enabled scenario** in the registry and, for each scenario whose cadence interval has elapsed since its last run, SHALL run the scenario with (a) the most recent moments from the store, capped at the scenario's `TrailSize`, and (b) the most recent prior nudges emitted by the same scenario, capped at the scenario's `PriorNudgesSize`. A scenario MAY decline to emit; declining SHALL store nothing.

#### Scenario: All enabled scenarios are evaluated each tick

- **WHEN** the tick handler runs after a successful capture
- **THEN** every scenario in `ScenarioRegistry.All` is checked for `IsDue(now)`, and each due scenario is executed in order

#### Scenario: Scenario runs receive their own trail size

- **WHEN** a scenario with `TrailSize=20` and another with `TrailSize=60` both run on the same tick
- **THEN** each is invoked with a trail of the appropriate length (the orchestrator calls `MomentStore.RecentAsync(scenario.TrailSize)` per scenario)

#### Scenario: Scenario runs receive their own prior-nudges context

- **WHEN** a scenario runs
- **THEN** it is invoked with `NudgeStore.RecentByScenarioAsync(scenario.Key, scenario.PriorNudgesSize)` — only nudges previously emitted by the same scenario, newest-first

#### Scenario: Scenario emits a nudge

- **WHEN** a scenario's structured output is `{"emit": true, "title": "...", "body": "...", "sources": [...]}`
- **THEN** a row is inserted with that `title`, `body`, and `sources` (serialized as JSON), and a new ULID `id` and current UTC `ts`

#### Scenario: Scenario stays silent

- **WHEN** a scenario's structured output is `{"emit": false, "reason": "..."}`
- **THEN** no row is inserted into the `nudges` table; the reason is captured for diagnostic display

### Requirement: LinkedIn Posts scenario

The system SHALL ship with one enabled scenario in this change, identified by the key `linkedin-posts`. It SHALL run no more often than once per hour (re-evaluated in-memory per app launch), it SHALL read the 20 most recent moments as its trail, and it SHALL use the model `claude-sonnet-4-6` with structured output following the schema in `design.md` D6. The system prompt SHALL match `design.md` D5 verbatim, framing the user as a principal-level software architect drafting AI-assisted-development thought leadership.

#### Scenario: Scenario runs at startup once

- **WHEN** the app launches and the LinkedIn scenario has not yet run in this process
- **THEN** the scenario is evaluated on the next successful moment tick

#### Scenario: Scenario is throttled at one hour

- **WHEN** the scenario has run within the last hour (in-memory `s_lastRun`)
- **THEN** subsequent ticks do not run the scenario until 60 minutes have elapsed

### Requirement: Nudge card

Each nudge SHALL be rendered as a `NudgeCard` containing, top to bottom: a scenario tag (colored dot + the scenario's `DisplayName`, looked up by `nudge.Scenario` via `ScenarioRegistry.GetByKey`), the nudge title (semibold, primary text color), and the nudge body (regular weight, secondary text color, wrapping). If the registry returns no match for `nudge.Scenario`, the tag SHALL fall back to the upper-cased scenario key and the default violet dot. The card SHALL NOT show any action affordances in this change.

#### Scenario: Card pulls display from the registry

- **WHEN** a nudge card renders with `nudge.Scenario = "achievements"`
- **THEN** the tag reads `ACHIEVEMENTS` and the colored dot uses the `AccentColorHex` registered by the Achievements scenario

#### Scenario: Card falls back when scenario is unknown

- **WHEN** a nudge card renders with a `nudge.Scenario` that does not match any registered scenario
- **THEN** the tag reads the upper-cased scenario key and the dot uses the default violet color

### Requirement: Manual scenario trigger

The Nudges tab section header SHALL include a play-glyph button at its right edge that, when clicked, runs **every scenario in the registry immediately**, bypassing each scenario's cadence throttle. The button SHALL be disabled while the run is in flight. After completion, a short inline status SHALL summarize the aggregated outcome: `Run complete: N emitted, M silent` when at least one nudge was emitted, `Silent: <first scenario's reason>` when no nudges were emitted but a scenario produced a reason, or `Scenario stayed silent` as a fallback. Each scenario's `_lastRun` SHALL be updated by a manual run as if it were a scheduled run.

#### Scenario: Click runs every scenario without waiting for throttles

- **WHEN** the user clicks the button (whether or not any scenario is due)
- **THEN** every scenario in the registry is executed once; the button is disabled until all return

#### Scenario: Aggregate status reflects the outcome

- **WHEN** the manual run completes with `e` scenarios emitting and `s` scenarios silent
- **THEN** the status reads `Run complete: e emitted, s silent` (or one of the documented fallback variants when nothing emitted)

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

### Requirement: Scenario abstraction

The system SHALL define an abstract `Scenario` base class encapsulating the per-scenario throttle (`Cadence`, in-memory `_lastRun`), trail / prior-nudges sizing (`TrailSize`, `PriorNudgesSize`), visual identity (`DisplayName`, `AccentColorHex`), **model selection (`ModelId`, defaulting to `Model.ClaudeSonnet4_6` and overridable per subclass)**, and the call template (`public Task<ScenarioResult> RunAsync(trail, priorNudges, ct)` that updates `_lastRun` and serializes concurrent attempts; `protected abstract Task<ScenarioResult> ExecuteAsync(trail, priorNudges, ct)` for the scenario-specific Claude call). The system SHALL provide a `ScenarioRegistry` static class with `IReadOnlyList<Scenario> All` and `Scenario? GetByKey(string key)`. The list of enabled scenarios MAY be hardcoded; the registry SHALL be the single source of truth for the panel orchestration and the UI.

#### Scenario: Adding a scenario requires only a class + registry entry

- **WHEN** a new `Scenario`-derived class is added with its own `Key`, `Cadence`, `TrailSize`, `Execute…`, and is appended to `ScenarioRegistry.All`
- **THEN** the panel orchestration, the manual trigger, and the nudge card pick it up with no other changes

#### Scenario: Scenario can override the model

- **WHEN** a scenario overrides `ModelId` to return a non-default model
- **THEN** its Claude call uses that model and the scenario diagnostic log records the model used per run

### Requirement: Achievement tracker scenario

The system SHALL ship a built-in scenario with key `achievements`, running at hourly cadence with `TrailSize = 60` and `PriorNudgesSize = 20`. Its system prompt SHALL ask Claude to identify one concrete achievement — shipped / decided / resolved / learned / moved — from the recent trail that has not already been emitted (per the prior-nudges context). The prompt SHALL instruct: plain past tense for completed things, present for ongoing decisions; no emojis; no motivational framing; hedge when ambiguous, commit when clear. When the trail shows nothing new, the scenario SHALL emit `{"emit": false, "reason": "..."}`. When it does emit, `title` SHALL be the achievement in one line, `body` SHALL be 1–2 sentences of context, and `sources` SHALL be the moment IDs that show the achievement.

#### Scenario: Achievements is registered and runs on the tick

- **WHEN** the tick handler runs and the Achievements scenario is due
- **THEN** it is executed with a 60-moment trail and up to 20 prior achievement nudges as context

#### Scenario: Dedup via prior nudges

- **WHEN** the trail still shows the same achievement that was emitted earlier
- **THEN** the scenario stays silent (the prior-nudges context reminds the model not to repeat itself)

#### Scenario: Display tag and dot

- **WHEN** an Achievements nudge renders
- **THEN** the card shows the tag `ACHIEVEMENTS` and a teal dot (`#54D2A6`)

### Requirement: LinkedIn Posts scenario uses an Opus-tier model

The LinkedIn Posts scenario SHALL declare `ModelId = Model.ClaudeOpus4_8`. Post quality matters disproportionately to cost at one-call-per-hour cadence, and the Opus tier is better suited than Sonnet for the "find the sharp opinion in the noise" shape this scenario asks for.

#### Scenario: LinkedIn call uses Opus 4.8

- **WHEN** the LinkedIn scenario runs (scheduled or manual)
- **THEN** the Claude call's `model` field is `claude-opus-4-8` and the corresponding `scenarios.log` block records that model

