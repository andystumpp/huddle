## MODIFIED Requirements

### Requirement: Scenario abstraction

The system SHALL define an abstract `Scenario` base class encapsulating the per-scenario throttle (`Cadence`, in-memory `_lastRun`), trail / prior-nudges sizing (`TrailSize`, `PriorNudgesSize`), visual identity (`DisplayName`, `AccentColorHex`), **model selection (`ModelId`, defaulting to `Model.ClaudeSonnet4_6` and overridable per subclass)**, and the call template (`public Task<ScenarioResult> RunAsync(trail, priorNudges, ct)` that updates `_lastRun` and serializes concurrent attempts; `protected abstract Task<ScenarioResult> ExecuteAsync(trail, priorNudges, ct)` for the scenario-specific Claude call). The system SHALL provide a `ScenarioRegistry` static class with `IReadOnlyList<Scenario> All` and `Scenario? GetByKey(string key)`. The list of enabled scenarios MAY be hardcoded; the registry SHALL be the single source of truth for the panel orchestration and the UI.

#### Scenario: Adding a scenario requires only a class + registry entry

- **WHEN** a new `Scenario`-derived class is added with its own `Key`, `Cadence`, `TrailSize`, `Execute…`, and is appended to `ScenarioRegistry.All`
- **THEN** the panel orchestration, the manual trigger, and the nudge card pick it up with no other changes

#### Scenario: Scenario can override the model

- **WHEN** a scenario overrides `ModelId` to return a non-default model
- **THEN** its Claude call uses that model and the scenario diagnostic log records the model used per run

## ADDED Requirements

### Requirement: LinkedIn Posts scenario uses an Opus-tier model

The LinkedIn Posts scenario SHALL declare `ModelId = Model.ClaudeOpus4_8`. Post quality matters disproportionately to cost at one-call-per-hour cadence, and the Opus tier is better suited than Sonnet for the "find the sharp opinion in the noise" shape this scenario asks for.

#### Scenario: LinkedIn call uses Opus 4.8

- **WHEN** the LinkedIn scenario runs (scheduled or manual)
- **THEN** the Claude call's `model` field is `claude-opus-4-8` and the corresponding `scenarios.log` block records that model
