## MODIFIED Requirements

### Requirement: A custom scenario is defined inline in configuration

A custom scenario definition SHALL carry a `key`, a `systemPrompt`, and optional presentation and execution settings: `displayName`, `accentColorHex`, `cadenceHours`, `trailSize`, `priorNudgesSize`, `model`, `effort`, and `webSearch`. Only `key` and `systemPrompt` SHALL be required; every other field SHALL default (`displayName` from the key, a neutral accent color, a default cadence, trail size, prior-nudge count, the default model, no effort, and web search off). The `systemPrompt` MAY be given either as a single string or as an array of strings; when it is an array, the elements SHALL be joined with newlines into one prompt (one element per line), and both forms SHALL yield an identical prompt. The `systemPrompt` SHALL describe only when the scenario emits, when it stays silent, and in what voice — it SHALL NOT need to describe the output JSON.

#### Scenario: A minimal definition runs with defaults

- **WHEN** a custom definition provides only `key` and `systemPrompt`
- **THEN** the scenario runs using default presentation and execution settings, producing nudges on the default cadence

#### Scenario: A full definition uses its provided settings

- **WHEN** a custom definition sets `cadenceHours`, `trailSize`, `model`, `effort`, and `accentColorHex`
- **THEN** the scenario runs on that cadence, over that trail size, with that model and effort, and its nudge card shows that accent color

#### Scenario: A multi-line prompt is written as an array of lines

- **WHEN** a custom definition's `systemPrompt` is an array of strings
- **THEN** the elements are joined with newlines into a single prompt, identical to writing that prompt as one string with `\n` line breaks

## ADDED Requirements

### Requirement: The configuration file may be annotated

The `huddle.config.json` parser SHALL accept single-line (`//`) and block (`/* */`) comments and trailing commas, so the file can be annotated and hand-edited without breaking parsing. Comments and trailing commas SHALL have no effect on the resulting configuration.

#### Scenario: A commented config parses

- **WHEN** `huddle.config.json` contains `//` comments and a trailing comma
- **THEN** it parses successfully and yields the same configuration as the comment-free equivalent
