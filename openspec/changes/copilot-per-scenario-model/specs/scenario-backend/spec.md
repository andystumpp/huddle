## MODIFIED Requirements

### Requirement: The Copilot CLI provider

The Copilot/Agency provider SHALL invoke the configured command in non-interactive mode (`-p`) with suppressed decoration so stdout is only the response (`-s`), a model (`--model`), and non-interactive behavior (`--no-ask-user`). Model names are **provider-relative**: the provider SHALL use the scenario's own model when it is a Copilot-native name, and SHALL fall back to the configured top-level model when the scenario's model is a bare Claude alias (`opus`/`sonnet`/`haiku`) or blank — because Copilot rejects those aliases while the built-in scenarios and the default use them. Because Copilot has no separate system-prompt option, the system prompt, user message, and JSON-schema directive SHALL be combined. Because the combined prompt can exceed the command-line length limit (e.g. the Learnings trail), the provider SHALL write the combined prompt to a temporary file and have the CLI read it with a narrow read-only tool grant, then delete that file after the call. On success the standard output SHALL be parsed into a `NudgeDraft` the same way as the Claude provider; a non-zero exit SHALL yield no completion.

#### Scenario: A large-prompt scenario runs on Copilot

- **WHEN** the Learnings scenario (a large trail) runs with the Copilot provider
- **THEN** the combined prompt is written to a temporary file, the CLI reads it under a read-only grant, its stdout deserializes into a `NudgeDraft`, and the temporary file is deleted

#### Scenario: Structured output over Copilot

- **WHEN** the request carries a JSON schema
- **THEN** the combined prompt instructs a single JSON object matching that schema, and the returned text deserializes into a `NudgeDraft`

#### Scenario: A Copilot-native per-scenario model is used

- **WHEN** a scenario's model is a Copilot-native name (for example `claude-opus-5`)
- **THEN** the provider invokes `--model` with that name, giving per-scenario model control on Copilot

#### Scenario: A bare Claude alias falls back to the configured model

- **WHEN** a scenario's model is a bare Claude alias (`opus`/`sonnet`/`haiku`) or blank
- **THEN** the provider invokes `--model` with the configured top-level model instead, so the scenario still runs on Copilot
