## 1. Seam: web-search capability

- [x] 1.1 Add `bool WebSearch = false` to `ScenarioRequest` in `src/Huddle.App/Scenarios/IScenarioBackend.cs` (optional, defaults off so existing callers are unchanged).

## 2. CliBackend: agentic web search

- [x] 2.1 In `CliBackend.CompleteAsync`, when `request.WebSearch` is true, append `--tools WebSearch WebFetch` and `--dangerously-skip-permissions` to the `claude` argv (availability limited to read-only search; permissions bypassed because an allow-list alone does not run tools headless).
- [x] 2.2 Use a longer timeout for web-search requests (the agentic loop is slower than a plain completion) — a conservative constant separate from the default.
- [x] 2.3 Leave the key scrub, model alias, schema-directive, plain-text stdout, and exit-code handling unchanged (inherited by both paths).

## 3. Efficiency Insights: CLI-only, single call

- [x] 3.1 Rewrite `EfficiencyInsightsScenario.ExecuteAsync` to a single CLI call: use a `CliBackend` unconditionally (ignore `HUDDLE_SCENARIO_BACKEND`), build one `ScenarioRequest` (Opus, generous MaxTokens, combined research+synthesize system prompt + `NudgeDraft` schema, `Effort: Effort.High`, `WebSearch: true`) with a user prompt that **forces** the model to call web search and ground the recommendation in a retrieved source; parse `NudgeDraft` and build the `Nudge` like the trail-only scenarios. Log via `ScenarioDiagnostics.LogRun`.
- [x] 3.2 Delete the two-phase API code: remove the `AnthropicClient`, `WebSearchTool20260209`, the separate research/synthesis `Messages.Create` calls, and merge `ResearchSystemPrompt` + `SynthesisSystemPrompt` into one combined prompt. Remove now-unused `using`s.
- [x] 3.3 Confirm the produced `Nudge` shape is identical to the previous path (same fields, ULID, sources).

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` is clean.
- [x] 4.2 CLI live run: launched the app; Efficiency Insights fired on the first tick, spawned one Huddle-owned `claude.exe` child (watched by parent PID), completed in ~90 s, and logged a run at `2026-07-27T11:24:24Z` with `usage: input=? output=?` (CLI signature). Grounded nudge emitted.
- [x] 4.3 Real search confirmed: the emitted nudge cites specific, current retrieved facts — "ProjectDiscovery from a 7% hit rate to 84%... cut LLM spend 59–70%", `kion.io/prompt-caching-reduce-ai-api-costs`, plus the Anthropic prompt-caching docs. That specificity is the fingerprint of a live search, not training memory.
- [x] 4.4 No metered API call: `grep` of `EfficiencyInsightsScenario.cs` for `AnthropicClient` / `Messages.Create` / `WebSearchTool` returns nothing — the two-phase API path is fully removed.
- [x] 4.5 Recorded below.

## Verification

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Live CLI run** — Launched `Huddle.exe`; on the first capture tick Efficiency Insights ran through its dedicated `CliBackend` (CLI-only, independent of `HUDDLE_SCENARIO_BACKEND`). Monitored **only Huddle's own child processes by ParentProcessId** (never `claude` by name): one `claude.exe` child appeared and completed in ~90 s. `scenarios.log` shows the run at `11:24:24Z` with `usage: input=? output=?` — the off-meter/subscription signature.

**Emitted nudge (proves a real search, and D4 forcing worked):**
> *"Cut Huddle's runaway vision spend at the source with Anthropic prompt caching instead of watching the balance drop"* — body cites `platform.claude.com/docs`, `kion.io/prompt-caching-reduce-ai-api-costs`, and the ProjectDiscovery 7%→84% / 59–70% cost-cut stat; sources are real trail moment IDs. The specific retrieved figures indicate an actual web search, not memory. (It even read the trail and recommended prompt caching to fix the exact vision-spend problem observed in the moments.)

**No metered API** — `EfficiencyInsightsScenario.cs` no longer references `AnthropicClient` / `Messages.Create` / `WebSearchTool20260209` (grep clean).

_Cosmetic note: em-dashes render as mojibake when reading `scenarios.log` in a non-UTF-8 terminal; the stored string is UTF-8 and unaffected._
