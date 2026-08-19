## ADDED Requirements

### Requirement: The active scenario set is composed from built-ins and configuration

The system SHALL assemble the set of scenarios it runs from the built-in scenarios plus a non-secret `scenarios` section in `huddle.config.json`, resolved with the same precedence as the rest of the configuration and read once at startup. The section is OPTIONAL and has two parts, both defaulting to empty: `disabled` (a list of built-in scenario keys to remove) and `custom` (inline scenario definitions to add). The active set SHALL be the built-in scenarios whose key is not in `disabled`, followed by one scenario per valid `custom` definition.

#### Scenario: No configuration runs the built-in scenarios

- **WHEN** `huddle.config.json` has no `scenarios` section
- **THEN** the four built-in scenarios (LinkedIn, Achievements, Learnings, Efficiency) run exactly as before this change

#### Scenario: A built-in scenario is disabled

- **WHEN** `scenarios.disabled` contains a built-in key (for example `linkedin-posts`)
- **THEN** that scenario does not run, and the remaining built-ins are unaffected

#### Scenario: A custom scenario is added

- **WHEN** `scenarios.custom` contains a valid definition with a new key
- **THEN** that scenario runs alongside the enabled built-ins on its configured cadence

### Requirement: A custom scenario is defined inline in configuration

A custom scenario definition SHALL carry a `key`, a `systemPrompt`, and optional presentation and execution settings: `displayName`, `accentColorHex`, `cadenceHours`, `trailSize`, `priorNudgesSize`, `model`, `effort`, and `webSearch`. Only `key` and `systemPrompt` SHALL be required; every other field SHALL default (`displayName` from the key, a neutral accent color, a default cadence, trail size, prior-nudge count, the default model, no effort, and web search off). The `systemPrompt` SHALL describe only when the scenario emits, when it stays silent, and in what voice — it SHALL NOT need to describe the output JSON.

#### Scenario: A minimal definition runs with defaults

- **WHEN** a custom definition provides only `key` and `systemPrompt`
- **THEN** the scenario runs using default presentation and execution settings, producing nudges on the default cadence

#### Scenario: A full definition uses its provided settings

- **WHEN** a custom definition sets `cadenceHours`, `trailSize`, `model`, `effort`, and `accentColorHex`
- **THEN** the scenario runs on that cadence, over that trail size, with that model and effort, and its nudge card shows that accent color

### Requirement: A custom scenario runs through the shared nudge pipeline

A custom scenario SHALL execute through the same pipeline as the built-in trail scenarios: it reads the recent moment trail and prior nudges, issues one completion through the configured provider carrying its `systemPrompt`, `model`, optional `effort`, and `webSearch`, and the returned text SHALL be parsed into a `NudgeDraft` and produce the same `Nudge` shape as a built-in scenario. The `NudgeDraft` JSON contract SHALL be enforced by the pipeline, not by the config-authored prompt.

#### Scenario: A custom scenario emits a nudge

- **WHEN** a custom scenario's completion returns an emitting `NudgeDraft`
- **THEN** a `Nudge` with the same shape as a built-in scenario's nudge is stored and shown, tagged with the custom scenario's key

#### Scenario: A custom scenario stays silent

- **WHEN** a custom scenario's `systemPrompt` directs it to stay silent for the current trail
- **THEN** it emits no nudge for that run, exactly as a built-in no-emit

### Requirement: Invalid custom definitions are skipped without affecting the others

When a custom definition is invalid — missing `key` or `systemPrompt`, a `key` that collides with a built-in key or an earlier custom key, or an unrecognized `model` or `effort` value — the system SHALL skip that definition and log a warning, and SHALL still run every valid built-in and custom scenario. One invalid definition SHALL NOT prevent capture or the other scenarios from running.

#### Scenario: A malformed definition is skipped

- **WHEN** one `custom` entry is missing its `systemPrompt` and another entry is valid
- **THEN** the malformed entry is skipped with a logged warning and the valid entry still runs

#### Scenario: A colliding key is rejected

- **WHEN** a `custom` entry uses a key that already belongs to a built-in scenario
- **THEN** that entry is skipped with a logged warning and the built-in with that key continues to run
