## MODIFIED Requirements

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

### Requirement: Manual scenario trigger

The Nudges tab section header SHALL include a play-glyph button at its right edge that, when clicked, runs **every scenario in the registry immediately**, bypassing each scenario's cadence throttle. The button SHALL be disabled while the run is in flight. After completion, a short inline status SHALL summarize the aggregated outcome: `Run complete: N emitted, M silent` when at least one nudge was emitted, `Silent: <first scenario's reason>` when no nudges were emitted but a scenario produced a reason, or `Scenario stayed silent` as a fallback. Each scenario's `_lastRun` SHALL be updated by a manual run as if it were a scheduled run.

#### Scenario: Click runs every scenario without waiting for throttles

- **WHEN** the user clicks the button (whether or not any scenario is due)
- **THEN** every scenario in the registry is executed once; the button is disabled until all return

#### Scenario: Aggregate status reflects the outcome

- **WHEN** the manual run completes with `e` scenarios emitting and `s` scenarios silent
- **THEN** the status reads `Run complete: e emitted, s silent` (or one of the documented fallback variants when nothing emitted)

### Requirement: Nudge card

Each nudge SHALL be rendered as a `NudgeCard` containing, top to bottom: a scenario tag (colored dot + the scenario's `DisplayName`, looked up by `nudge.Scenario` via `ScenarioRegistry.GetByKey`), the nudge title (semibold, primary text color), and the nudge body (regular weight, secondary text color, wrapping). If the registry returns no match for `nudge.Scenario`, the tag SHALL fall back to the upper-cased scenario key and the default violet dot. The card SHALL NOT show any action affordances in this change.

#### Scenario: Card pulls display from the registry

- **WHEN** a nudge card renders with `nudge.Scenario = "achievements"`
- **THEN** the tag reads `ACHIEVEMENTS` and the colored dot uses the `AccentColorHex` registered by the Achievements scenario

#### Scenario: Card falls back when scenario is unknown

- **WHEN** a nudge card renders with a `nudge.Scenario` that does not match any registered scenario
- **THEN** the tag reads the upper-cased scenario key and the dot uses the default violet color

## ADDED Requirements

### Requirement: Scenario abstraction

The system SHALL define an abstract `Scenario` base class encapsulating the per-scenario throttle (`Cadence`, in-memory `_lastRun`), trail / prior-nudges sizing (`TrailSize`, `PriorNudgesSize`), visual identity (`DisplayName`, `AccentColorHex`), and the call template (`public Task<ScenarioResult> RunAsync(trail, priorNudges, ct)` that updates `_lastRun` and serializes concurrent attempts; `protected abstract Task<ScenarioResult> ExecuteAsync(trail, priorNudges, ct)` for the scenario-specific Claude call). The system SHALL provide a `ScenarioRegistry` static class with `IReadOnlyList<Scenario> All` and `Scenario? GetByKey(string key)`. The list of enabled scenarios MAY be hardcoded in this change; the registry SHALL be the single source of truth for the panel orchestration and the UI.

#### Scenario: Adding a scenario requires only a class + registry entry

- **WHEN** a new `Scenario`-derived class is added with its own `Key`, `Cadence`, `TrailSize`, `Execute…`, and is appended to `ScenarioRegistry.All`
- **THEN** the panel orchestration, the manual trigger, and the nudge card pick it up with no other changes

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
