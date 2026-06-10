## 1. Scenario implementation

- [x] 1.1 Create `src/Huddle.App/Scenarios/LearningsScenario.cs`, mirroring `AchievementsScenario.cs`. Override `Key = "learnings"`, `Name = "Learnings"`, `DisplayName = "LEARNINGS"`, `AccentColorHex = "#F5C56C"`, `Cadence = TimeSpan.FromHours(24)`, `TrailSize = 200`, `PriorNudgesSize = 5`, `ModelId = Model.ClaudeOpus4_8`.
- [x] 1.2 Write the `SystemPrompt` per `design.md` D6 — focus on the *learned* axis (new pattern adopted / belief updated / gotcha discovered / heuristic refined / new tool learned); explicit boundary from Achievements ("what got done" vs "how understanding changed"); plain past-tense second-person; no emojis; no motivational framing; hedge when ambiguous; stay silent with a concrete one-sentence `reason` when the day shows no learning thread.
- [x] 1.3 Implement `ExecuteAsync` identically to `AchievementsScenario.ExecuteAsync` — `AnthropicClient`, `MessageCreateParams` with `Model = ModelId`, `MaxTokens = 600`, `JsonOutputFormat` using `ScenarioPromptHelpers.BuildNudgeDraftSchema()`, deserialize `NudgeDraft`, return `ScenarioResult`. Call `ScenarioDiagnostics.LogRun(Key, ModelId.ToString(), SystemPrompt, userText, text, response.Usage?.InputTokens, response.Usage?.OutputTokens)`.
- [x] 1.4 Implement `BuildUserText` calling `ScenarioPromptHelpers.AppendPriorNudges(sb, priorNudges, now, "Previously emitted today")` then `ScenarioPromptHelpers.AppendRecentMoments(sb, trail, now, TrailSize)`, then a closing instruction line: `"Identify ONE concrete learning from the trail above, or stay silent per the system prompt. Sources should reference moment IDs from the trail (e.g. \"01KTQ...\")."`.

## 2. Wire-up

- [x] 2.1 In `src/Huddle.App/Scenarios/ScenarioRegistry.cs`, append `new LearningsScenario()` to the `All` array after `new AchievementsScenario()`.

## 3. Verification

- [x] 3.1 `dotnet build Huddle.slnx -c Debug` succeeds with zero errors.
- [ ] 3.2 Launch the app and click the "Run scenarios now" play button. Confirm a `learnings` block appears in `%LOCALAPPDATA%\Huddle\scenarios.log` with model `claude-opus-4-8`.
- [ ] 3.3 If the Learnings call emits, confirm the new nudge appears at the top of the Nudges tab with the tag `LEARNINGS` and a warm-amber dot. If it stays silent (acceptable — depends on trail content), confirm the `reason` field of the log block is non-empty and concrete.
- [ ] 3.4 Manually re-trigger via "Run scenarios now" a second time. Confirm the Learnings scenario is throttled (does not re-run) — the run status reports it as already-due-and-handled, OR run a no-op (cadence skip).
