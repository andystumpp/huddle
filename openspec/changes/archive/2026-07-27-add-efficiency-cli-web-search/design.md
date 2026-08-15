## Context

`add-cli-scenario-backend` introduced `IScenarioBackend` with `ApiBackend` and `CliBackend`, and a narrow `ScenarioRequest` (`Model`, `MaxTokens`, `SystemPrompt`, `UserText`, `JsonSchema`, `Effort?`). It routed Learnings/Achievements/LinkedIn onto the CLI but left **Efficiency Insights** on the API (design D6), because that scenario's phase-1 uses the API `WebSearchTool` server tool, which the text seam can't express.

Efficiency Insights is now the dominant API cost — Opus 4.8, two phases, and web results that inflate input (observed `input=222,148`, ~$3+/run), every 6 h. Verification established:

- Headless `claude -p` **can** web search, and it's **off the metered API** (the CLI's client-side WebSearch tool; the API's `usage.server_tool_use.web_search_requests` stays 0).
- It only actually searches with **`--dangerously-skip-permissions`** — `--allowed-tools WebSearch` alone yields permission events and no search in print mode.
- After the agentic loop it still returns a **single parseable `NudgeDraft` JSON**.
- One run answered from memory and **fabricated** a citation — so the search must be forced.

Decision from review: make Efficiency Insights **CLI-only** and **delete** its two-phase API path (no fallback). If the CLI is unavailable, the scenario simply no-emits — the same graceful degradation every CLI scenario already has.

## Goals / Non-Goals

**Goals:**
- Move Efficiency Insights fully onto the subscription — CLI-only, metered two-phase API path removed.
- Add web search to the seam with the smallest honest surface (one bool), bounding the dangerous flag by restricting tool availability.
- Keep the produced `Nudge` identical to before.

**Non-Goals:**
- Vision on the CLI (separate change).
- General CLI tool use beyond read-only search.
- Keeping any API path for Efficiency Insights (deliberately removed).

## Sequence

Efficiency Insights is now CLI-only: one agentic, web-search-enabled call that collapses the old research+synthesis phases into a single turn, then the shared `NudgeDraft` parse. No backend branch, no API path.

```mermaid
sequenceDiagram
    participant Sc as EfficiencyInsightsScenario
    participant Cli as CliBackend
    participant Proc as claude (child)

    rect rgb(245,245,245)
    Note over Sc,Proc: 1. Single agentic web-search call
    Sc->>Cli: CompleteAsync(ScenarioRequest{WebSearch:true, Effort:High, Opus, schema})
    Cli->>Cli: args += --tools WebSearch WebFetch --dangerously-skip-permissions; longer timeout
    Cli->>Cli: Environment.Remove("ANTHROPIC_API_KEY")
    Cli->>Proc: claude -p <trail + "you MUST search"> --model opus --append-system-prompt <sys+schema>
    Proc->>Proc: agentic loop: WebSearch → results → emit JSON
    alt exit 0
        Proc-->>Cli: stdout = NudgeDraft JSON
        Cli-->>Sc: BackendResult(Text: JSON, null, null)
    else non-zero / claude missing
        Proc-->>Cli: stderr
        Cli-->>Sc: BackendResult(null, …) → scenario no-emits
    end
    end

    rect rgb(245,245,245)
    Note over Sc: 2. Parse → Nudge
    Sc->>Sc: JsonSerializer.Deserialize<NudgeDraft>(text)
    Sc->>Sc: emit gate → Nudge, or no-emit(reason)
    end
```

### 1. Single agentic web-search call

**Contract** — Out to backend: `ScenarioRequest { Model = Opus, MaxTokens (generous), SystemPrompt (combined research+synthesize+schema), UserText (trail + a directive that the model MUST search), JsonSchema = NudgeDraft, Effort = High, WebSearch = true }`. Back: `BackendResult { Text = NudgeDraft JSON | null, InTok = null, OutTok = null }`.

**How** — The scenario uses a `CliBackend` unconditionally (it does not consult `HUDDLE_SCENARIO_BACKEND` — this scenario is CLI-only by nature). `CliBackend`, seeing `WebSearch = true`, appends `--tools WebSearch WebFetch --dangerously-skip-permissions` and raises the timeout above the plain-call value (agentic search is slower). `--tools` limits *availability* to the two read-only search tools, so the bypassed permissions can't reach `Bash`/`Write`/`Edit`. The key scrub (subscription auth) and plain-text stdout handling are inherited from the base CLI path. `claude` runs its client-side WebSearch (off-meter), then emits the schema-instructed JSON as its final message = stdout. If `claude` is missing or exits non-zero, `Text` is null and the scenario no-emits for that tick.

### 2. Parse → Nudge

**Contract** — In: the JSON text. Out: `ScenarioResult(Nudge? , string? Reason)`. `NudgeDraft` = `emit` (bool, required) + `reason`/`title`/`body` (string?) + `sources` (string[]?).

**How** — Identical to every other scenario: `JsonSerializer.Deserialize<NudgeDraft>(text)`, then the emit gate builds a `Nudge(ULID, now, key, title, body, sources)` or returns a no-emit reason. Same `Nudge` shape as the old two-phase path produced.

## Decisions

### D1: Web search is one bool on the seam, not a tool list

`ScenarioRequest` gains `bool WebSearch = false`. **Alternative:** a general `Tools` collection — rejected (YAGNI, and the CLI's tool story differs from the API's). One bool is the whole current need; the CLI maps it to a fixed, safe flag set.

### D2: Bound `--dangerously-skip-permissions` by restricting `--tools`

The flag is required (verified: allow-list alone won't run tools headless) but broad. Restricting availability to `WebSearch WebFetch` means the bypass can only ever reach read-only search — no shell or file writes exist for that invocation. This is the mitigation the user accepted.

### D3: Efficiency Insights is CLI-only; the two-phase API path is deleted

Reviewed and chosen over keeping a backend-branching fallback. The scenario always uses a `CliBackend` with `WebSearch = true`, regardless of `HUDDLE_SCENARIO_BACKEND`. This removes all backend-specific branching, deletes the `AnthropicClient`/`WebSearchTool20260209` two-phase code, and keeps the seam contract uniform for every scenario. **Trade-off accepted:** Efficiency Insights needs a working, logged-in `claude` CLI; without it, it no-emits (same as any CLI scenario). It no longer runs on a bare funded API key — that's the point (it was the biggest metered cost).

### D4: Force the search in the prompt

Because a run fabricated a source with zero searches, the CLI prompt explicitly requires calling web search and grounding in a retrieved source. Chosen over a hard tool-ran assertion (the plain-text CLI path can't observe tool events); the user confirmed prompt-forcing is sufficient.

## Risks / Trade-offs

- **[Model answers from memory, fabricating grounding]** → D4 forces the search in the prompt; residual risk accepted for a 6-hourly background nudge.
- **[`--dangerously-skip-permissions` is broad]** → D2 restricts `--tools` to read-only search, so the blast radius is only WebSearch/WebFetch.
- **[CLI missing / not logged in]** → Efficiency Insights no-emits that tick (no API fallback by design); acceptable, matches other CLI scenarios.
- **[Agentic search latency]** → longer CLI timeout; fine at a 6-hour cadence in the background.
- **[This session's classifier blocks the exact `--tools … --dangerously-skip-permissions` combo]** → so the app is the first place it runs end-to-end; verify live during apply.

## Migration Plan

No data migration. Efficiency Insights always takes the CLI web-search path once this ships; there is no flag to set for it (it ignores `HUDDLE_SCENARIO_BACKEND`). Rollback is reverting the change (which restores the two-phase API path from git history) — not a runtime toggle.

## Open Questions

- Timeout value for the agentic CLI call (research loops longer than a plain completion) — pick a conservative default during apply and confirm against a real run.
