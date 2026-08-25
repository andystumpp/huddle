## MODIFIED Requirements

### Requirement: A scenario is defined inline in configuration

A scenario definition SHALL carry a `key` and a `systemPrompt`, plus optional settings: `displayName`, `accentColorHex`, `cadenceHours`, `trailSize`, `priorNudgesSize`, `model`, `effort`, `webSearch`, and `showInAll`. Only `key` and `systemPrompt` SHALL be required; every other field SHALL default (`displayName` from the key, a neutral accent color, a default cadence, trail size, prior-nudge count, the default model, no effort, web search off, and `showInAll` true). The `systemPrompt` MAY be given either as a single string or as an array of strings joined with newlines into one prompt. The `systemPrompt` SHALL describe only when the scenario emits, when it stays silent, and in what voice — it SHALL NOT need to describe the output JSON.

#### Scenario: A minimal definition runs with defaults

- **WHEN** a definition provides only `key` and `systemPrompt`
- **THEN** the scenario runs using default presentation and execution settings, including `showInAll` true

#### Scenario: A full definition uses its provided settings

- **WHEN** a definition sets `cadenceHours`, `trailSize`, `model`, `effort`, and `accentColorHex`
- **THEN** the scenario runs on that cadence, over that trail size, with that model and effort, and its nudge card shows that accent color

#### Scenario: A multi-line prompt is written as an array of lines

- **WHEN** a definition's `systemPrompt` is an array of strings
- **THEN** the elements are joined with newlines into a single prompt, identical to writing it as one string with `\n` line breaks

#### Scenario: A scenario opts out of the All view

- **WHEN** a definition sets `showInAll` to `false`
- **THEN** the scenario runs normally and emits nudges, but those nudges are kept out of the `All` view (they remain visible under the scenario's own filter)
