## ADDED Requirements

### Requirement: Scenario Claude calls run through a selectable backend

Scenario Claude calls SHALL be dispatched through an `IScenarioBackend` abstraction that exposes a single completion operation taking a system prompt, one text user message, a model, a max-token cap, and an optional JSON output schema, and returning the response text together with input and output token counts for diagnostics. The system SHALL provide two implementations: an API backend that calls the Anthropic SDK, and a CLI backend that invokes the local `claude` executable. The vision path (`MomentExtractor`) SHALL continue to call the Anthropic SDK directly and SHALL NOT use this abstraction.

#### Scenario: A trail-only scenario emits through the backend

- **WHEN** the Learnings, Achievements, or LinkedIn scenario runs and produces a nudge
- **THEN** the Claude call is made via the resolved `IScenarioBackend`, and the returned text is parsed into a `NudgeDraft` exactly as before, producing the same `Nudge` shape

#### Scenario: Diagnostics still record token usage

- **WHEN** a scenario completes a backend call
- **THEN** `ScenarioDiagnostics.LogRun` receives the scenario key, model, prompts, response text, and the input/output token counts reported by the backend

### Requirement: Backend selection is configuration-driven and defaults to the API

The backend SHALL be selected by a configuration flag `HUDDLE_SCENARIO_BACKEND` with the values `api` or `cli`, resolved from the same sources as `ANTHROPIC_API_KEY` (process environment, User registry environment target, and the `huddle.env` file candidates). When the flag is absent, empty, or unrecognized, the system SHALL use the API backend, so behavior is unchanged until the user opts in.

#### Scenario: Default is the API backend

- **WHEN** no `HUDDLE_SCENARIO_BACKEND` value is configured
- **THEN** scenarios use the API backend and calls are billed against `ANTHROPIC_API_KEY`, identical to prior behavior

#### Scenario: Opting into the CLI backend

- **WHEN** `HUDDLE_SCENARIO_BACKEND=cli` is configured
- **THEN** trail-only scenarios dispatch their Claude call through the CLI backend

#### Scenario: Unrecognized value falls back to the API

- **WHEN** `HUDDLE_SCENARIO_BACKEND` is set to a value other than `api` or `cli`
- **THEN** the system uses the API backend rather than failing

### Requirement: The CLI backend invokes Claude Code on the user's subscription

The CLI backend SHALL invoke the `claude` executable in print mode (`-p`) with the user message as the prompt, `--append-system-prompt` carrying the scenario's system prompt, and `--model` set to the CLI model alias mapped from the scenario's model (`ClaudeOpus4_8` → `opus`, `ClaudeSonnet4_6` → `sonnet`). It SHALL use the default plain-text output: standard output is the assistant's response string, returned as the completion text with null token counts. The child process SHALL be launched with `ANTHROPIC_API_KEY` removed from its environment so that Claude Code authenticates against the user's subscription rather than the metered API key.

#### Scenario: The child process does not inherit the API key

- **WHEN** the CLI backend spawns `claude`
- **THEN** the child process environment does not contain `ANTHROPIC_API_KEY`, even though the parent process has promoted that variable into its own environment

#### Scenario: Model alias is mapped for the CLI

- **WHEN** a scenario whose model is `ClaudeOpus4_8` runs on the CLI backend
- **THEN** the `claude` invocation passes `--model opus`

#### Scenario: The response is the plain-text stdout

- **WHEN** `claude` exits with code 0
- **THEN** the backend returns standard output as the completion text, with both token counts null

#### Scenario: A failed invocation yields no completion

- **WHEN** `claude` exits with a non-zero code (for example, not logged in or the OAuth token revoked)
- **THEN** the backend returns no completion text and the scenario emits nothing for that run

### Requirement: The completion request always carries a JSON output schema

Every scenario completion request SHALL include a JSON output schema describing the `NudgeDraft` object; the field is required, not optional. The API backend SHALL enforce it through the SDK's structured-output configuration. Because the CLI has no structured-output parameter, the CLI backend SHALL append instructions to the system prompt directing the model to respond with a single JSON object conforming to that schema and nothing else. In both cases the scenario SHALL parse the returned text into its `NudgeDraft` using the same deserialization.

#### Scenario: The API backend enforces the schema

- **WHEN** a scenario runs on the API backend
- **THEN** the request sets the structured-output format to the `NudgeDraft` schema, and the response text deserializes into a `NudgeDraft`

#### Scenario: The CLI backend requests the schema in the prompt

- **WHEN** a scenario runs on the CLI backend
- **THEN** the system prompt sent to `claude` instructs it to emit only a JSON object matching the `NudgeDraft` schema, and the returned text deserializes into a `NudgeDraft` with the expected fields
