## REMOVED Requirements

### Requirement: The active scenario set is composed from built-ins and configuration

**Reason**: There are no built-in scenarios any more — the scenario set comes entirely from configuration. Replaced by "The active scenario set comes from configuration".

**Migration**: Provide scenarios in the `scenarios` array of `huddle.config.json` (copy `huddle.config.example.json` for the former defaults). `disabled` is gone (there is nothing built-in to disable).

### Requirement: A custom scenario is defined inline in configuration

**Reason**: With no built-ins there is no "custom" vs "built-in" distinction — every scenario is defined inline. Replaced by "A scenario is defined inline in configuration".

**Migration**: None — the field set is unchanged; definitions now live in the `scenarios` array rather than under `scenarios.custom`.

### Requirement: A custom scenario runs through the shared nudge pipeline

**Reason**: Renamed for the config-only model. Replaced by "A configured scenario runs through the nudge pipeline" (same behavior).

**Migration**: None.

### Requirement: Invalid custom definitions are skipped without affecting the others

**Reason**: Renamed and simplified (no built-in-key collision case). Replaced by "Invalid scenario definitions are skipped without affecting the others".

**Migration**: None.

## ADDED Requirements

### Requirement: The active scenario set comes from configuration

The system SHALL build the set of scenarios it runs entirely from a non-secret `scenarios` **array** in `huddle.config.json`, resolved with the same precedence as the rest of the configuration and read once at startup. Each array element is a scenario definition. There are no built-in scenarios. When the array is absent or empty, no scenarios run — moments are still captured, but no nudges are produced.

#### Scenario: No scenarios configured produces no nudges

- **WHEN** `huddle.config.json` has no `scenarios` array (or an empty one)
- **THEN** no scenarios run and no nudges are produced, while moment capture continues

#### Scenario: Configured scenarios run

- **WHEN** the `scenarios` array contains valid definitions
- **THEN** each runs on its configured cadence, in array order

### Requirement: A scenario is defined inline in configuration

A scenario definition SHALL carry a `key` and a `systemPrompt`, plus optional settings: `displayName`, `accentColorHex`, `cadenceHours`, `trailSize`, `priorNudgesSize`, `model`, `effort`, and `webSearch`. Only `key` and `systemPrompt` SHALL be required; every other field SHALL default (`displayName` from the key, a neutral accent color, a default cadence, trail size, prior-nudge count, the default model, no effort, and web search off). The `systemPrompt` MAY be given either as a single string or as an array of strings joined with newlines into one prompt. The `systemPrompt` SHALL describe only when the scenario emits, when it stays silent, and in what voice — it SHALL NOT need to describe the output JSON.

#### Scenario: A minimal definition runs with defaults

- **WHEN** a definition provides only `key` and `systemPrompt`
- **THEN** the scenario runs using default presentation and execution settings

#### Scenario: A full definition uses its provided settings

- **WHEN** a definition sets `cadenceHours`, `trailSize`, `model`, `effort`, and `accentColorHex`
- **THEN** the scenario runs on that cadence, over that trail size, with that model and effort, and its nudge card shows that accent color

#### Scenario: A multi-line prompt is written as an array of lines

- **WHEN** a definition's `systemPrompt` is an array of strings
- **THEN** the elements are joined with newlines into a single prompt, identical to writing it as one string with `\n` line breaks

### Requirement: A configured scenario runs through the nudge pipeline

Each configured scenario SHALL execute through the same pipeline: it reads the recent moment trail and prior nudges, issues one completion through the configured provider carrying its `systemPrompt`, `model`, optional `effort`, and `webSearch`, and the returned text SHALL be parsed into a `NudgeDraft` and produce a `Nudge` tagged with the scenario's key. The `NudgeDraft` JSON contract SHALL be enforced by the pipeline, not by the config-authored prompt.

#### Scenario: A scenario emits a nudge

- **WHEN** a scenario's completion returns an emitting `NudgeDraft`
- **THEN** a `Nudge` is stored and shown, tagged with the scenario's key

#### Scenario: A scenario stays silent

- **WHEN** a scenario's `systemPrompt` directs it to stay silent for the current trail
- **THEN** it emits no nudge for that run

### Requirement: Invalid scenario definitions are skipped without affecting the others

When a scenario definition is invalid — missing `key` or `systemPrompt`, a `key` that duplicates an earlier definition, or an unrecognized `model` or `effort` value — the system SHALL skip that definition and log a warning, and SHALL still run every valid scenario. One invalid definition SHALL NOT prevent capture or the other scenarios from running.

#### Scenario: A malformed definition is skipped

- **WHEN** one entry is missing its `systemPrompt` and another entry is valid
- **THEN** the malformed entry is skipped with a logged warning and the valid entry still runs

#### Scenario: A duplicate key is skipped

- **WHEN** two entries share the same `key`
- **THEN** the later one is skipped with a logged warning and the first still runs

### Requirement: A default example configuration is provided

The repository SHALL include a committed `huddle.config.example.json` that provides a ready configuration reproducing the former default scenarios (Achievements, Learnings, LinkedIn posts, Efficiency insights) with their prompts and settings. Copying it to the resolved configuration path SHALL yield those scenarios unchanged.

#### Scenario: The example reproduces the former defaults

- **WHEN** `huddle.config.example.json` is copied to `huddle.config.json` unchanged
- **THEN** the four former built-in scenarios run with the same prompts, cadences, models, and settings as before this change
