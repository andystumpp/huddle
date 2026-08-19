## MODIFIED Requirements

### Requirement: Scenario Claude calls run through a selectable backend

Scenario calls SHALL be dispatched through an `IScenarioBackend` abstraction that exposes a single completion operation taking a system prompt, one text user message, a model name, a max-token cap, and an optional JSON output schema, and returning the response text together with input and output token counts for diagnostics (token counts MAY be null when a provider does not report them). The abstraction SHALL be implemented **only by local CLI providers** — there is no API/SDK backend. The provider is chosen from configuration (see "Provider selection comes from configuration").

#### Scenario: A trail-only scenario emits through the backend

- **WHEN** the Learnings, Achievements, or LinkedIn scenario runs and produces a nudge
- **THEN** the call is made via the resolved CLI `IScenarioBackend`, and the returned text is parsed into a `NudgeDraft` exactly as before, producing the same `Nudge` shape

#### Scenario: Diagnostics still record the run

- **WHEN** a scenario completes a backend call
- **THEN** `ScenarioDiagnostics.LogRun` receives the scenario key, model, prompts, and response text (token counts when the provider reports them, otherwise null)

## REMOVED Requirements

### Requirement: Backend selection is configuration-driven and defaults to the API

**Reason**: There is no longer an API/SDK backend to default to — every scenario runs on a local CLI provider. Selection now names a CLI provider (`claude` | `copilot` | `agency`) rather than choosing between API and CLI.

**Migration**: Set `scenarios.provider` in `huddle.config.json` (see "Provider selection comes from configuration"). The old `HUDDLE_SCENARIO_BACKEND=cli` behavior maps to `provider: "claude"`.

## ADDED Requirements

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

### Requirement: The Copilot CLI provider

The Copilot/Agency provider SHALL invoke the configured command in non-interactive mode (`-p`) with suppressed decoration so stdout is only the response (`-s`), the configured model (`--model`), and non-interactive behavior (`--no-ask-user`). Because Copilot has no separate system-prompt option, the system prompt, user message, and JSON-schema directive SHALL be combined. Because the combined prompt can exceed the command-line length limit (e.g. the Learnings trail), the provider SHALL write the combined prompt to a temporary file and have the CLI read it with a narrow read-only tool grant, then delete that file after the call. On success the standard output SHALL be parsed into a `NudgeDraft` the same way as the Claude provider; a non-zero exit SHALL yield no completion.

#### Scenario: A large-prompt scenario runs on Copilot

- **WHEN** the Learnings scenario (a large trail) runs with the Copilot provider
- **THEN** the combined prompt is written to a temporary file, the CLI reads it under a read-only grant, its stdout deserializes into a `NudgeDraft`, and the temporary file is deleted

#### Scenario: Structured output over Copilot

- **WHEN** the request carries a JSON schema
- **THEN** the combined prompt instructs a single JSON object matching that schema, and the returned text deserializes into a `NudgeDraft`

### Requirement: Web search is provider-dependent

Web-search grounding for scenarios that request it (Efficiency Insights) SHALL be performed only on providers that expose a web-search capability. On a provider without web search, the scenario SHALL either run without grounding or be skipped, and SHALL NOT present an ungrounded answer as if it had searched.

#### Scenario: Web search on a capable provider

- **WHEN** Efficiency Insights runs on a provider that supports web search
- **THEN** the provider performs the search and grounds the recommendation in a retrieved source

#### Scenario: No web search available

- **WHEN** Efficiency Insights runs on a provider without web search
- **THEN** it does not fabricate a grounded citation — it runs ungrounded or emits nothing
