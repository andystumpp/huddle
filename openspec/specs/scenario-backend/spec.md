# scenario-backend Specification

## Purpose

Selecting and constructing the local CLI provider that both scenario calls and vision run through — one of `claude`, `copilot`, or `agency`, chosen from a non-secret `huddle.config.json` — including CLI child-process construction, environment-key scrubbing, JSON-schema handling, and config-driven provider selection. There is no API/SDK backend; each provider authenticates through its own CLI login.

## Requirements

### Requirement: Scenario Claude calls run through a selectable backend

Scenario calls SHALL be dispatched through the completion operation of an `ICliProvider` abstraction (the same abstraction that serves the vision path). That operation takes a system prompt, one text user message, a model name, a max-token cap, and an optional JSON output schema, and returns the response text together with input and output token counts for diagnostics (token counts MAY be null when a provider does not report them). The abstraction SHALL be implemented **only by local CLI providers** — there is no API/SDK backend. The provider is chosen from configuration (see "Provider selection comes from configuration").

#### Scenario: A trail-only scenario emits through the backend

- **WHEN** the Learnings, Achievements, or LinkedIn scenario runs and produces a nudge
- **THEN** the call is made via the resolved CLI `ICliProvider`, and the returned text is parsed into a `NudgeDraft` exactly as before, producing the same `Nudge` shape

#### Scenario: Diagnostics still record the run

- **WHEN** a scenario completes a backend call
- **THEN** `ScenarioDiagnostics.LogRun` receives the scenario key, model, prompts, and response text (token counts when the provider reports them, otherwise null)

### Requirement: Provider selection comes from configuration

The scenario provider and its settings SHALL be read from a non-secret `huddle.config.json`, resolved with the same precedence as other Huddle configuration (process environment location, User target, the exe directory, then `%LOCALAPPDATA%\Huddle\`). The configuration SHALL name the active provider (`claude` | `copilot` | `agency`); the executable command and model are OPTIONAL and SHALL default (command to the provider's conventional binary name; the Copilot/Agency model to `claude-opus-5`), so naming only the provider is a complete configuration. The file SHALL contain no secrets — each CLI authenticates through its own login.

#### Scenario: Minimal configuration selects Copilot with defaults

- **WHEN** `huddle.config.json` contains only `{ "provider": "copilot" }`
- **THEN** scenarios (and vision) run on the `copilot` command with model `claude-opus-5`, with no other settings required

#### Scenario: Agency reuses the Copilot invocation

- **WHEN** the provider is `agency`
- **THEN** the same Copilot-style invocation is used with the configured Agency command

#### Scenario: Default provider when unconfigured

- **WHEN** no provider is configured
- **THEN** the `claude` CLI provider is used

### Requirement: The Claude CLI provider invokes Claude Code on the user's subscription

The Claude provider SHALL invoke the `claude` executable in print mode (`-p`) with the user message fed on standard input (a large trail exceeds the Windows command-line length limit as an argument), `--append-system-prompt` carrying the scenario's system prompt with the JSON-schema directive appended, and `--model` set to the CLI alias derived from the request's model-name string (a name containing `opus` → `opus`, `sonnet` → `sonnet`, `haiku` → `haiku`; an unrecognized name is rejected rather than guessed). It SHALL use the default plain-text output: standard output is the assistant's response string, returned as the completion text with null token counts. The child process SHALL be launched with `ANTHROPIC_API_KEY` removed from its environment so that Claude Code authenticates against the user's subscription rather than a metered API key.

#### Scenario: The child process does not inherit the API key

- **WHEN** the Claude provider spawns `claude`
- **THEN** the child process environment does not contain `ANTHROPIC_API_KEY`, even though the parent process has promoted that variable into its own environment

#### Scenario: Model alias is derived for the CLI

- **WHEN** a scenario whose model name contains `opus` runs on the Claude provider
- **THEN** the `claude` invocation passes `--model opus`

#### Scenario: The response is the plain-text stdout

- **WHEN** `claude` exits with code 0
- **THEN** the provider returns standard output as the completion text, with both token counts null

#### Scenario: A failed invocation yields no completion

- **WHEN** `claude` exits with a non-zero code (for example, not logged in or the OAuth token revoked)
- **THEN** the provider returns no completion text and the scenario emits nothing for that run

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

### Requirement: The completion request always carries a JSON output schema

Every scenario completion request SHALL include a JSON output schema describing the `NudgeDraft` object; the field is required, not optional. Because the CLI has no structured-output parameter, each provider SHALL append instructions to its prompt directing the model to respond with a single JSON object conforming to that schema and nothing else. The Claude provider appends the directive to its system prompt; the Copilot provider folds it into the combined prompt and then isolates the first balanced JSON object from stdout, because Copilot prefaces the object with conversational prose. In every case the scenario SHALL parse the returned text into its `NudgeDraft` using the same deserialization.

#### Scenario: A provider requests the schema in the prompt

- **WHEN** a scenario runs on the Claude provider
- **THEN** the system prompt sent to `claude` instructs it to emit only a JSON object matching the `NudgeDraft` schema, and the returned text deserializes into a `NudgeDraft` with the expected fields

#### Scenario: Copilot output is isolated to the first JSON object

- **WHEN** a scenario runs on the Copilot provider and stdout wraps the JSON object in conversational prose
- **THEN** the provider isolates the first balanced JSON object from stdout and that text deserializes into a `NudgeDraft`

### Requirement: A completion request may request web search

The `ScenarioRequest` SHALL carry an optional web-search flag, default off. When off, the request behaves as a plain completion. When on, it signals that the scenario needs the provider to ground its answer in live web results before producing the response text.

#### Scenario: Trail-only scenarios are unaffected

- **WHEN** a scenario that does not set the web-search flag runs
- **THEN** the request and the provider behave as a plain completion, unchanged by this capability

#### Scenario: A scenario requests web search

- **WHEN** a scenario sets the web-search flag and runs on a provider that supports web search
- **THEN** the provider performs an agentic web search and returns the assistant's final text (a `NudgeDraft` JSON object)

### Requirement: Web search is provider-dependent

Web-search grounding for scenarios that request it (Efficiency Insights) SHALL be performed only on providers that expose a web-search capability. On a provider without web search, the scenario SHALL either run without grounding or be skipped, and SHALL NOT present an ungrounded answer as if it had searched.

#### Scenario: Web search on a capable provider

- **WHEN** Efficiency Insights runs on a provider that supports web search
- **THEN** the provider performs the search and grounds the recommendation in a retrieved source

#### Scenario: No web search available

- **WHEN** Efficiency Insights runs on a provider without web search
- **THEN** it does not fabricate a grounded citation — it runs ungrounded or emits nothing

### Requirement: The Claude CLI provider performs an off-meter agentic web search

When the request enables web search, the Claude provider SHALL invoke `claude` with tool availability limited to read-only search (`--tools WebSearch WebFetch`) and with permissions bypassed (`--dangerously-skip-permissions`), because in headless print mode an allow-list alone does not execute tools. The search runs as the CLI's client-side WebSearch tool, which draws on the user's subscription. The provider SHALL allow a longer timeout than a plain call, since the agentic search loop is slower.

#### Scenario: The Claude provider searches the live web on the subscription

- **WHEN** a web-search request runs on the Claude provider
- **THEN** `claude` executes at least one `WebSearch` tool call as a client-side tool drawing on the user's subscription

#### Scenario: Only read-only search tools are available to the call

- **WHEN** the Claude provider runs a web-search request
- **THEN** the invocation exposes only `WebSearch` and `WebFetch`, so the bypassed permissions cannot reach file-writing or shell tools

#### Scenario: The searched result is still a parseable nudge

- **WHEN** the agentic search loop completes on exit code 0
- **THEN** standard output is a single JSON object that deserializes into a `NudgeDraft`

### Requirement: Efficiency Insights runs a single call and forces the search

The Efficiency Insights scenario SHALL issue one web-search-enabled completion on the configured provider that researches and emits the `NudgeDraft` in a single turn, and its prompt SHALL require the model to actually search rather than answer from memory. It SHALL produce the same `Nudge` shape as before.

#### Scenario: A single web-search-enabled call on the configured provider

- **WHEN** Efficiency Insights runs
- **THEN** it makes a single web-search-enabled completion on the configured provider with high effort and produces the same `Nudge` shape as before

#### Scenario: The prompt forces a real search

- **WHEN** the scenario builds its prompt
- **THEN** the prompt instructs the model that it must call web search and ground the recommendation in a retrieved source, so it does not answer from training memory

#### Scenario: No provider result means no emission

- **WHEN** Efficiency Insights runs and the configured CLI is missing or not logged in
- **THEN** the call returns no text and the scenario emits nothing for that run
