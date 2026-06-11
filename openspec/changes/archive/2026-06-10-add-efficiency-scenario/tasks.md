## 1. Scenario scaffold

- [x] 1.1 Create `src/Huddle.App/Scenarios/EfficiencyInsightsScenario.cs` deriving from `Scenario`. Override `Key = "efficiency-insights"`, `Name = "Efficiency insights"`, `DisplayName = "EFFICIENCY"`, `AccentColorHex = "#6BA6FF"`, `Cadence = TimeSpan.FromHours(6)`, `TrailSize = 60`, `PriorNudgesSize = 10`, `ModelId = Model.ClaudeOpus4_8`.
- [x] 1.2 Define the two system prompts per `design.md` D5 as `const string` fields: `ResearchSystemPrompt` (infer the user's stack/workflow, research current dev workflow & tooling best practice incl. community sources, gather don't recommend) and `SynthesisSystemPrompt` (name ONE concrete actionable improvement specific to this user, "you do X → proven better Y because Z (source)", stay silent over generic advice). Both include the boundary statement vs Achievements/Learnings and the no-emoji / no-motivational / hedge rules.

## 2. Phase 1 — web research

- [x] 2.1 Implement `BuildResearchUserText(trail, priorNudges, now)` using `ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously recommended (do not repeat)")` then `ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize)`, plus a closing line asking Claude to research applicable dev-efficiency practices and summarize findings.
- [x] 2.2 In `ExecuteAsync`, build the research `MessageCreateParams`: `Model = ModelId`, generous `MaxTokens`, `System = ResearchSystemPrompt`, `Thinking = new ThinkingConfigAdaptive()`, `OutputConfig = new OutputConfig { Effort = Effort.High }` (no `Format`), `Tools = [ new WebSearchTool20260209 { MaxUses = 5 } ]`, and the user message from 2.1.
- [x] 2.3 Call `client.Messages.Create` once. Per design D3, do **not** implement a `pause_turn` round-trip — `MaxUses = 5` keeps research to a single response. Capture `response.StopReason` for diagnostics only.
- [x] 2.4 Collect phase-1 findings as the concatenation of all `TextBlock` text from the response (`response.Content.Select(b => b.Value).OfType<TextBlock>()`). The scenario never reads or constructs server-tool block types.
- [x] 2.5 Call `ScenarioDiagnostics.LogRun` for the research phase (key `efficiency-insights`), recording `ModelId.ToString()`, the research prompt, the research user text, the findings text, and usage.

## 3. Phase 2 — synthesis into NudgeDraft

- [x] 3.1 Build `BuildSynthesisUserText(trail, findings, now)` — include the trail context (so `sources` can reference moment IDs) and the phase-1 findings text, with a closing instruction to emit ONE improvement or stay silent per the synthesis prompt.
- [x] 3.2 Build the synthesis `MessageCreateParams`: `Model = ModelId`, `MaxTokens` sized for the draft, `System = SynthesisSystemPrompt`, `Thinking = new ThinkingConfigAdaptive()`, `OutputConfig = new OutputConfig { Effort = Effort.High, Format = new JsonOutputFormat { Schema = ScenarioPromptHelpers.BuildNudgeDraftSchema() } }`, **no `Tools`**, and the user message from 3.1.
- [x] 3.3 Call `client.Messages.Create`, extract the first `TextBlock`, `JsonSerializer.Deserialize<NudgeDraft>`, and map to `ScenarioResult` exactly as `LinkedInPostsScenario` does (silent → reason; emit with empty title/body → guard; else build `Nudge` with `UlidGenerator.Generate()`, `Key`, `draft.Title`/`Body`/`Sources`). `sources` are trail moment IDs; web citations stay in `body`.
- [x] 3.4 Call `ScenarioDiagnostics.LogRun` for the synthesis phase (e.g. key `efficiency-insights:synthesis`), recording the model, synthesis prompt, synthesis user text, raw response, and usage.

## 4. Wire-up

- [x] 4.1 In `src/Huddle.App/Scenarios/ScenarioRegistry.cs`, append `new EfficiencyInsightsScenario()` to the `All` array after `new LearningsScenario()`.

## 5. Verification

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` succeeds with zero errors and zero warnings.
- [x] 5.2 Launch the app and click the "Run scenarios now" play button. Confirm an `efficiency-insights` research block AND a synthesis block appear in `%LOCALAPPDATA%\Huddle\scenarios.log`, both with model `claude-opus-4-8`. Confirm the research block shows web search activity / cited sources.
- [x] 5.3 If the scenario emits, confirm the new nudge appears at the top of the Nudges tab with the tag `EFFICIENCY` and a cool-blue dot, the body names a proven approach + source, and `sources` reference trail moment IDs. If it stays silent (acceptable — depends on trail), confirm the synthesis `reason` is non-empty and concrete.
- [x] 5.4 Click "Run scenarios now" again immediately and confirm the scenario is throttled (does not re-run before 6h).
