# scenario-config Specification

## Purpose

The active scenario set comes entirely from a `scenarios` array in `huddle.config.json` — there are no built-in scenarios, so an empty (or absent) config yields no scenarios and only moments are captured. Each configured scenario runs through the shared trail → provider → `NudgeDraft` pipeline, so a config-authored scenario produces a `Nudge` without touching code. A committed `huddle.config.example.json` carries the default scenarios ready to copy. The scenario filter pills and each nudge card's scenario label are derived from the configured scenarios.

## Requirements

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

### Requirement: The configuration file may be annotated

The `huddle.config.json` parser SHALL accept single-line (`//`) and block (`/* */`) comments and trailing commas, so the file can be annotated and hand-edited without breaking parsing. Comments and trailing commas SHALL have no effect on the resulting configuration.

#### Scenario: A commented config parses

- **WHEN** `huddle.config.json` contains `//` comments and a trailing comma
- **THEN** it parses successfully and yields the same configuration as the comment-free equivalent

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

### Requirement: Scenario display is derived from the configured scenarios

The scenario filter pills and each nudge card's scenario label SHALL be derived from the configured scenarios, not a fixed list. The filter pills SHALL be a leading "All" pill plus one pill per configured scenario, labelled by its `displayName`. A scenario's `displayName` SHALL be stored in natural case; it SHALL be shown as-is on the filter pill and uppercased on the nudge card tag.

#### Scenario: Filter pills match the configured scenarios

- **WHEN** the scenario set is configured
- **THEN** the filter pills are "All" plus one pill per configured scenario; a scenario removed from the config has no pill, and one added has a pill

#### Scenario: displayName is natural case, uppercased on the card

- **WHEN** a scenario's `displayName` is natural case (for example `LinkedIn posts`)
- **THEN** the filter pill shows it as-is and the nudge card tag shows it uppercased (`LINKEDIN POSTS`)
