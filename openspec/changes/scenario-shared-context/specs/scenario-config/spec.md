## ADDED Requirements

### Requirement: Shared context is prepended to every scenario

The configuration MAY include a top-level `context` — a string, or an array of lines joined with newlines. When it is present and non-empty, it SHALL be prepended to every configured scenario's system prompt (before the scenario's own `systemPrompt`), so all scenarios share the same context without repeating it. It SHALL always be applied — there is no per-scenario opt-out. When `context` is absent or empty, scenarios SHALL run exactly as without it. The shared context SHALL apply to scenarios only, not to the vision/moment prompt.

#### Scenario: Context is prepended to a scenario's prompt

- **WHEN** `context` is set and a scenario runs
- **THEN** the scenario's system prompt begins with the context, followed by the scenario's own `systemPrompt`, and the completion is produced as normal

#### Scenario: No context is a no-op

- **WHEN** `context` is absent or empty
- **THEN** every scenario runs with exactly the system prompt it would have had without this feature

#### Scenario: Context does not affect vision

- **WHEN** a moment is captured
- **THEN** the vision prompt does not include the shared `context`
