## Why

Efficiency Insights is Huddle's single largest API cost: Opus 4.8, two phases, and web-search results that balloon input (one observed run at **222,148 input tokens**, ~$3+), every 6 hours. The prior change (`add-cli-scenario-backend`) moved the three trail-only scenarios onto the subscription but had to leave Efficiency Insights on the metered API, because its research phase uses the API `WebSearchTool` server tool, which the plain-text seam couldn't express (design D6, deferred). Verification has since confirmed the local `claude` CLI can perform web search **headless and off the metered API** (client-side WebSearch tool; the API's `web_search_requests` counter stays 0) and still return a clean `NudgeDraft` JSON. So the biggest line item can now move to the subscription.

## What Changes

- Extend the `IScenarioBackend` seam with a **web-search capability**: add `bool WebSearch` (default `false`) to `ScenarioRequest`.
- `CliBackend`: when `WebSearch` is true, invoke `claude` with `--tools WebSearch WebFetch --dangerously-skip-permissions` (tool *availability* limited to read-only search; permissions bypassed because `--allowed-tools` alone does not run tools headless — verified) and a longer timeout for the agentic search loop.
- **Efficiency Insights becomes CLI-only**: it runs as a **single agentic call** on the CLI (research + synthesize + emit `NudgeDraft` JSON in one turn, `WebSearch: true`, `Effort.High`), regardless of `HUDDLE_SCENARIO_BACKEND`. The CLI has no citations-vs-JSON conflict, so the API's two-phase split collapses to one call. The existing **two-phase API path is deleted** — no fallback; if the CLI is unavailable the scenario no-emits (like every CLI scenario).
- The scenario **forces a real search** in its prompt so it can't silently answer from memory (a verification run fabricated a citation without searching).
- Vision (`MomentExtractor`) and the three already-ported scenarios are unchanged.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `scenario-backend`: Adds an optional web-search capability to the completion request; defines how the CLI backend performs an off-meter agentic web search and how a scenario selects the single-call CLI path versus a backend-specific fallback.

## Impact

- **Depends on** the unarchived `add-cli-scenario-backend` change (this modifies the `scenario-backend` capability and the `IScenarioBackend`/`ScenarioRequest`/`CliBackend` types it introduced). Land or archive that first.
- **Modified code**: `IScenarioBackend.cs` (`ScenarioRequest.WebSearch`), `CliBackend.cs` (web-search flags + longer timeout), `EfficiencyInsightsScenario.cs` (CLI-preferred single-call branch, keep two-phase API fallback).
- **Unchanged**: vision, the three trail-only scenarios, storage, UI.
- **External**: relies on the `claude` CLI's client-side WebSearch (subscription, off-meter). `--dangerously-skip-permissions` is bounded by restricting `--tools` to read-only search only.
- **Risk to handle**: the model must actually search rather than answer from memory — the scenario prompt forces it, and the design notes verifying a tool ran.
