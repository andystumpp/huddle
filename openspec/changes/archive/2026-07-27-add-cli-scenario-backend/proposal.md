## Why

Every Claude call in Huddle authenticates with `ANTHROPIC_API_KEY` and is billed per-token against the metered API. The user already runs the Claude Code CLI, logged in via their subscription. Scenario calls — the app's most expensive spend (Opus, large trails, web research) — are infrequent and cadence-throttled, so their latency is not on any hot path. Routing them through the local CLI draws on the subscription instead of the meter, cutting API cost, while the per-tick vision path stays on the API where it belongs.

## What Changes

- Introduce an `IScenarioBackend` seam: a single completion call (system prompt + one text user message + optional JSON output schema + model + max-token cap → response text + input/output token counts). This is the second real implementation of the Claude call, which is the point at which CLAUDE.md permits an interface.
- Add two backends:
  - `ApiBackend` — today's `AnthropicClient` + `Messages.Create` path, lifted out unchanged.
  - `CliBackend` — shells out to `claude -p <userText> --model <opus|sonnet> --append-system-prompt <system> --output-format json`, parsing the JSON envelope's result text and usage.
- Route the three trail-only scenarios (**Learnings**, **Achievements**, **LinkedIn**) through the backend. `EfficiencyInsights` keeps calling the API directly for now — its phase-1 web-search server tool does not fit the plain-text seam (deferred, see design).
- Add a config flag `HUDDLE_SCENARIO_BACKEND` (`api` | `cli`), read from the existing `huddle.env` resolution chain, **defaulting to `api`** so behavior is unchanged until the user opts in.
- Vision (`MomentExtractor`) is **not** changed — it stays on the API SDK.

## Capabilities

### New Capabilities
- `scenario-backend`: Selecting and constructing the Claude backend that scenario calls run through — the API SDK (metered) or the local Claude Code CLI (subscription) — including CLI child-process construction, environment-key scrubbing, JSON-schema handling, and config-driven selection.

### Modified Capabilities
<!-- None. `nudges` requirements (storage, emission, cadence, per-scenario trail/model) are unchanged; this change only alters HOW a scenario's Claude call is dispatched, which the nudges spec does not currently constrain. -->

## Impact

- **New code**: `src/Huddle.App/Scenarios/IScenarioBackend.cs`, `ApiBackend.cs`, `CliBackend.cs`, and a small config resolver for the backend flag.
- **Modified code**: `Scenario` base class (owns/resolves the backend); `LearningsScenario`, `AchievementsScenario`, `LinkedInPostsScenario` (call the backend instead of `new AnthropicClient()` directly).
- **Unchanged**: `MomentExtractor` (vision), `EfficiencyInsightsScenario`, `NudgeStore`, `ScenarioDiagnostics` surface, and all storage/UI.
- **External dependency**: the `claude` CLI on `PATH`, logged in via the user's subscription. When absent or not on the CLI backend, nothing changes.
- **Correctness risk to handle**: `MomentExtractor` promotes `ANTHROPIC_API_KEY` into the process environment; the CLI child must be launched with that variable scrubbed, or Claude Code would prefer it and silently bill the API — defeating the change.
