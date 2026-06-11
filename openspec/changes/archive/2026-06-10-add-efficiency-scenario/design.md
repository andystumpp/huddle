## Context

Three scenarios already use the `Scenario` abstraction ([Scenario.cs](../../../src/Huddle.App/Scenarios/Scenario.cs)): `LinkedInPostsScenario`, `AchievementsScenario`, `LearningsScenario`. All three are single-call: `ExecuteAsync` makes one `client.Messages.Create` over the moment trail and deserializes a `NudgeDraft`.

Efficiency Insights is the first scenario that needs information **not on the user's screen** — the current external landscape of dev practices and tooling. That forces two firsts for this codebase:

1. **A server-side tool.** The `Anthropic` SDK exposes web search as `WebSearchTool20260209` (GA, implicit-converts to `ToolUnion`). Claude issues queries; Anthropic runs them and returns results with citations.
2. **More than one Claude call per run.** Web search results carry citations, and structured JSON output (`output_config.format`) is **incompatible with citations** — combining them returns a 400. So the run is split into a research phase (tools, no JSON schema) and a synthesis phase (JSON schema, no tools).

`ExecuteAsync` is abstract and returns a `ScenarioResult`, so the subclass owns its internal control flow — the base class's throttle (`Cadence`, `_lastRun`) and concurrent-run gate still apply unchanged. No base-class change is needed.

## Goals / Non-Goals

**Goals:**
- Surface ONE concrete, actionable efficiency insight grounded in *external* best practice: "you keep doing X manually; framework/practice/tool Y is proven to help — here's why and a source."
- Research the live web (incl. community sources like Reddit) for the latest in dev workflow & tooling: spec-driven development, testing frameworks for agentic development, libraries/tools the user is underusing.
- Infer from the trail what the user is doing and how they currently work, then research what applies.
- Keep the storage + UI contract identical to the other scenarios — emit a `NudgeDraft`, store via `NudgeStore`, render via the existing `NudgeCard`.

**Non-Goals:**
- Persisting the 6-hour throttle across restarts. In-memory `_lastRun` is consistent with the other scenarios; prior-nudges dedup handles the restart case.
- Multiple insights per run. One nudge per run; stay silent if nothing applies.
- A separate tab/filter. The card tags by scenario via the registry; it surfaces in the unified Nudges list with the blue accent.
- Researching anything beyond dev workflow & tooling (the user chose this scope).
- A reusable web-research base class. This is the first web scenario — extract only when a second one needs it (CLAUDE.md: don't introduce abstraction before the second implementation).

## Decisions

### D1. Subclass `Scenario`; two-phase `ExecuteAsync`

```csharp
internal sealed class EfficiencyInsightsScenario : Scenario
{
    public override string Key => "efficiency-insights";
    public override string Name => "Efficiency insights";
    public override string DisplayName => "EFFICIENCY";
    public override string AccentColorHex => "#6BA6FF";
    public override TimeSpan Cadence => TimeSpan.FromHours(6);
    public override int TrailSize => 60;
    public override int PriorNudgesSize => 10;
    public override Model ModelId => Model.ClaudeOpus4_8;
    // ExecuteAsync: phase 1 research (web_search) -> phase 2 synthesis (JSON)
}
```

### D2. Phase 1 — web research with `WebSearchTool20260209`

`MessageCreateParams` with `Tools = [ new WebSearchTool20260209 { MaxUses = 5 } ]`, the research system prompt (D5), and a user message built from the trail + prior nudges. `Thinking = new ThinkingConfigAdaptive()`, `Effort.High` — query formulation and judging source quality is reasoning-heavy.

Web search runs server-side: Claude emits `server_tool_use` blocks, Anthropic executes them, and `web_search_tool_result` blocks come back in the same response. The `20260209` version does dynamic filtering automatically — no separate `code_execution` tool, no beta header.

The output of phase 1 that phase 2 consumes is the concatenation of the response's `TextBlock`s — Claude's prose synthesis of what it found, with the facts and source URLs it considers relevant.

### D3. `MaxUses` bounds research to a single response; no server-tool round-trip

Web search has a server-side iteration cap (~10). If Claude wanted more rounds, the response would come back with `StopReason == "pause_turn"`, and the documented way to continue is to re-send the assistant turn with its server-tool blocks preserved.

**Decision: don't implement that round-trip.** Set `MaxUses = 5` so the server completes its searches well under the cap and returns a single `end_turn` response. Phase 1 is therefore one `Messages.Create` call; the findings are the `TextBlock` text in that response. If `pause_turn` ever does come back, we proceed to synthesis with the findings gathered so far (and note it in diagnostics) rather than attempting to resume.

**Why:** reconstructing `ServerToolUseBlockParam` / `WebSearchToolResultBlockParam` by hand (there is no `.ToParam()` in the C# SDK) is fragile and version-sensitive, and it would only ever run on a path `MaxUses` makes unreachable. Five searches is ample for a focused efficiency-research task. This keeps the scenario from ever needing to read or construct server-tool block types — it only reads `TextBlock`s, exactly like the other scenarios. YAGNI: add resume if a real need for >5 searches appears.

### D4. Phase 2 — synthesis into `NudgeDraft`

A second `Messages.Create` with **no tools**, `OutputConfig.Format = JsonOutputFormat { Schema = ScenarioPromptHelpers.BuildNudgeDraftSchema() }`, `Thinking = new ThinkingConfigAdaptive()`, `Effort.High`. The user message carries (a) the original trail context and (b) phase 1's findings text. The synthesis prompt decides whether the findings justify a concrete, actionable insight and, if so, emits `{emit:true, title, body, sources}`; otherwise `{emit:false, reason}`.

Deserialize `NudgeDraft` and return `ScenarioResult` exactly as the other scenarios do. `sources` here are **moment IDs from the trail** (the work that motivated the insight), keeping the field's meaning consistent with the other scenarios — the web citations live in the `body` prose, not in `sources`.

### D5. Two system prompts, sharp scope and boundary

**Research prompt (phase 1):** "You see what the user has been doing. Infer their stack, workflow, and how they currently work. Research the *current* best practices in dev workflow & tooling that apply — spec-driven development, testing frameworks for agentic development, libraries/tools they appear not to be using. Prefer recent, credible, specific sources; community signal (Reddit, HN, maintainer threads) is in scope when it reflects real adoption. Summarize what you found and why it's relevant to *this* user. Don't recommend yet — gather."

**Synthesis prompt (phase 2):** "From the findings, name ONE concrete, actionable efficiency improvement. Shape: 'you do X this way → proven better way is Y, because Z (source).' It must be specific to how this user works, not generic advice. Title = the improvement in one line. Body = 1–2 sentences with the why and a named source/tool. If nothing in the findings rises above generic advice the user is likely already doing, stay silent with a concrete one-sentence reason."

Both: no emojis, no motivational framing, hedge when ambiguous.

**Boundary from the other scenarios** (stated in the prompt): Achievements = *what got done*; Learnings = *how your understanding changed*; Efficiency = *how you could work better, based on external best practice you may not know about*.

### D6. Cadence = 6h, TrailSize = 60, PriorNudgesSize = 10

6 hours (within the user's 4–6h choice) balances responsiveness against the higher per-run cost (web research + two Opus calls). 60 moments ≈ 3 hours of trail at the 3-min capture cadence — enough to read "how the user works" without the day-scale bulk Learnings needs. `PriorNudgesSize = 10` dedups across runs so the same recommendation isn't surfaced repeatedly.

### D7. Model = Opus 4.8

Forming good search queries, judging source credibility, and lifting findings into a recommendation specific to this user is the same "find the signal" shape as LinkedIn and Learnings, both of which use Opus.

### D8. Diagnostics

`ScenarioDiagnostics.LogRun` is called for **both** phases so the log shows the research conversation and the synthesis separately (e.g. keys `efficiency-insights` / `efficiency-insights:synthesis`, or one combined block) — the model used must be `claude-opus-4-8` in both.

### D9. Registry placement

Append `new EfficiencyInsightsScenario()` to `ScenarioRegistry.All` after `LearningsScenario`. Order only affects manual-trigger evaluation order; the panel sorts by `ts DESC`.

## Risks / Trade-offs

- **Risk:** `pause_turn` server-tool round-trip is fiddly in C# (manual block reconstruction).
  → Mitigation: `MaxUses = 5` keeps the common path to a single `end_turn` response. The continuation loop is bounded (~3) and best-effort; if reconstruction is imperfect, the scenario degrades to "synthesize from what we have," never crashes (the base `RunAsync` already swallows exceptions into a null result).

- **Risk:** Cost — two Opus calls plus multiple web searches every 6 hours is materially more than the trail-only scenarios.
  → Mitigation: 6-hour cadence bounds it to ~4 runs/day; `MaxUses` bounds searches per run; the existing screen-lock gate prevents runs while away.

- **Risk:** Recommendations drift generic ("write tests!") or repeat across runs.
  → Mitigation: the synthesis prompt demands specificity to *this* user's observed workflow and stay-silent-over-generic; `PriorNudgesSize = 10` feeds prior recommendations back in for dedup.

- **Risk:** Network/privacy — first scenario to make outbound web requests; trail content shapes the queries Claude writes.
  → Mitigation: Claude formulates queries about *practices/tools*, not the user's content; no raw moment text is sent to search engines by design. The behavior matches every other Claude call already sending trail summaries to the API.

- **Risk:** Citations-vs-structured-output incompatibility could resurface if someone later "simplifies" this to one call.
  → Mitigation: the two-phase split is documented here and in the spec as the reason, so the constraint isn't rediscovered the hard way.

## Open Questions

- Whether to log phase 1 and phase 2 as one combined `scenarios.log` block or two. Leaning two for debuggability; either satisfies the spec requirement that both record `claude-opus-4-8`. Resolve during implementation.
