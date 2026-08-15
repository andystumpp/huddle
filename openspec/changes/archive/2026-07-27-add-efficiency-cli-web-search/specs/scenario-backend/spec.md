## ADDED Requirements

### Requirement: A completion request may request web search

The `ScenarioRequest` SHALL carry an optional web-search flag, default off. When off, the request behaves exactly as today. When on, it signals that the scenario needs the backend to ground its answer in live web results before producing the response text.

#### Scenario: Trail-only scenarios are unaffected

- **WHEN** a scenario that does not set the web-search flag runs
- **THEN** the request and both backends behave identically to before this change

#### Scenario: A scenario requests web search

- **WHEN** a scenario sets the web-search flag and runs on the CLI backend
- **THEN** the backend performs an agentic web search and returns the assistant's final text (a `NudgeDraft` JSON object)

### Requirement: The CLI backend performs an off-meter agentic web search

When the request enables web search, the CLI backend SHALL invoke `claude` with tool availability limited to read-only search (`--tools WebSearch WebFetch`) and with permissions bypassed (`--dangerously-skip-permissions`), because in headless print mode an allow-list alone does not execute tools. The search runs as the CLI's client-side WebSearch tool, which draws on the user's subscription rather than the metered API. The backend SHALL allow a longer timeout than a plain call, since the agentic search loop is slower.

#### Scenario: The CLI searches the live web off the metered API

- **WHEN** a web-search request runs on the CLI backend
- **THEN** `claude` executes at least one `WebSearch` tool call and the metered API's server-side web-search counter is not incremented

#### Scenario: Only read-only search tools are available to the call

- **WHEN** the CLI backend runs a web-search request
- **THEN** the invocation exposes only `WebSearch` and `WebFetch`, so the bypassed permissions cannot reach file-writing or shell tools

#### Scenario: The searched result is still a parseable nudge

- **WHEN** the agentic search loop completes on exit code 0
- **THEN** standard output is a single JSON object that deserializes into a `NudgeDraft`

### Requirement: Efficiency Insights runs a single CLI call and forces the search

The Efficiency Insights scenario SHALL run only on the CLI: it SHALL issue one web-search-enabled completion that researches and emits the `NudgeDraft` in a single turn, regardless of the `HUDDLE_SCENARIO_BACKEND` value, and its prompt SHALL require the model to actually search rather than answer from memory. It SHALL produce the same `Nudge` shape as before.

#### Scenario: A single CLI call regardless of the backend flag

- **WHEN** Efficiency Insights runs
- **THEN** it makes a single web-search-enabled completion on the CLI with high effort — even when `HUDDLE_SCENARIO_BACKEND` is unset or `api` — and produces the same `Nudge` shape as before

#### Scenario: The prompt forces a real search

- **WHEN** the scenario builds its prompt
- **THEN** the prompt instructs the model that it must call web search and ground the recommendation in a retrieved source, so it does not answer from training memory

#### Scenario: No CLI available means no emission

- **WHEN** Efficiency Insights runs and `claude` is missing or not logged in
- **THEN** the call returns no text and the scenario emits nothing for that run (there is no metered API fallback)
