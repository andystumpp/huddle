## 1. Scenario abstraction

- [x] 1.1 Add `src/Huddle.App/Scenarios/Scenario.cs` — abstract base class per `design.md` D1, with `Key` / `Name` / `DisplayName` / `AccentColorHex` / `Cadence` / `TrailSize` / `PriorNudgesSize` abstract members, in-memory `_lastRun` + `SemaphoreSlim _gate`, the `IsDue(now)` predicate, the `RunAsync(trail, priorNudges, ct)` template, and the protected `ExecuteAsync(trail, priorNudges, ct)` abstract
- [x] 1.2 Move `ScenarioResult` from `LinkedInPostsScenario.cs` to its own file `src/Huddle.App/Scenarios/ScenarioResult.cs` so both scenarios share the type
- [x] 1.3 Add `src/Huddle.App/Scenarios/ScenarioRegistry.cs` — `static IReadOnlyList<Scenario> All` initialized with `new LinkedInPostsScenario()` + `new AchievementsScenario()`; `static Scenario? GetByKey(string)`

## 2. Refactor LinkedIn

- [x] 2.1 Convert `LinkedInPostsScenario` from `static class` to `internal sealed class : Scenario` and implement the abstract members (existing constants become property overrides)
- [x] 2.2 Rename `LinkedInPostsScenario.RunAsync(trail, ct)` to a protected override `ExecuteAsync(trail, priorNudges, ct)`. The base class handles `_lastRun` / locking now — drop the local `s_lastRun` field and `s_gate`
- [x] 2.3 Update the user-message builder to render a `Previously posted today (newest first):` block from `priorNudges` (when non-empty), placed before the `Recent moments` block. Format per `design.md` D3
- [x] 2.4 Keep the existing system prompt, JSON schema, scenario diagnostic logging, and `Reason` deserialization path unchanged

## 3. Add the Achievement scenario

- [x] 3.1 Add `src/Huddle.App/Scenarios/AchievementsScenario.cs` — `internal sealed class : Scenario` with `Key = "achievements"`, `Name = "Achievements"`, `DisplayName = "ACHIEVEMENTS"`, `AccentColorHex = "#54D2A6"`, `Cadence = TimeSpan.FromHours(1)`, `TrailSize = 60`, `PriorNudgesSize = 20`
- [x] 3.2 System prompt per `design.md` D4 — what counts as an achievement, voice rules, dedup instruction referencing the prior-nudges block, emit-vs-silent guidance, what `title` / `body` / `sources` should contain
- [x] 3.3 JSON-schema dictionary identical in shape to LinkedIn's (`emit`, `reason`, `title`, `body`, `sources` — only `emit` is required)
- [x] 3.4 `ExecuteAsync`: build user message including `Previously emitted (newest first):` block from `priorNudges` and `Recent moments (newest first):` from `trail`; call the SDK; parse `NudgeDraft`; return `ScenarioResult`. Reuse the same `LogRun` style diagnostic write to `scenarios.log` (add a small `LogRun` helper here or move the existing one into a shared `ScenarioDiagnostics` static class if cleaner)
- [x] 3.5 If parsing yields `emit:true` with empty title or body, return a silent result with reason `"Model emitted but title/body was empty"`

## 4. Store extension

- [x] 4.1 Add `NudgeStore.RecentByScenarioAsync(string scenario, int limit)` mirroring `RecentAsync` but with `WHERE scenario = $scenario` in the SQL. Reuse the JSON-deserialization for `sources`

## 5. Panel orchestration

- [x] 5.1 In `PeekPanelWindow.OnSchedulerTick`, replace the LinkedIn-specific scenario block with a `foreach (var scenario in ScenarioRegistry.All)` loop that checks `IsDue`, pulls the trail at `scenario.TrailSize` and the prior nudges at `scenario.PriorNudgesSize`, calls `scenario.RunAsync`, and handles emits identically to today
- [x] 5.2 Update `OnRunScenariosNowClick` to iterate every scenario in the registry (regardless of `IsDue`), accumulate `emitted` / `silent` counts and the first non-empty reason; status text becomes `Run complete: {emitted} emitted, {silent} silent` (with the existing fallbacks for the all-silent and silent-no-reason paths)

## 6. UI plumbing — `NudgeCard` reads the registry

- [x] 6.1 Drop the hardcoded `"LINKEDIN POSTS"` switch in `Controls/NudgeCard.xaml.cs`; replace with `ScenarioRegistry.GetByKey(Nudge.Scenario)` and use the `DisplayName` / `AccentColorHex` from the result, with the existing violet + uppercased-key fallback when null
- [x] 6.2 Update `ScenarioDot.Fill` to a `SolidColorBrush` parsed from the hex string (parse using a tiny `Color.FromArgb(...)` helper — same pattern as the existing `s_errorBrush` / `s_okBrush` static brushes in `PeekPanelWindow.xaml.cs`)

## 7. Verification

- [x] 7.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 7.2 Launch — the immediate-on-start tick fires the LinkedIn scenario (existing behavior) AND the Achievements scenario (new). Both either emit or stay silent
- [x] 7.3 If both stay silent on the first tick, the manual "Run scenarios now" button fires both. Status reads `Run complete: 0 emitted, 2 silent` when both are silent, `Run complete: 1 emitted, 1 silent` when one emits, etc.
- [x] 7.4 Inspect emitted rows: `scenario` column reads `linkedin-posts` or `achievements`; `NudgeCard` renders the right tag + dot color per scenario
- [x] 7.5 An Achievements card's body reads in past/present tense per the prompt rules, anchors in concrete moment details, no emojis or motivational framing
- [x] 7.6 Trigger the manual run a second time after one scenario emitted; that scenario's next call sees the prior nudge in its context and (per the prompt's dedup instruction) tends to stay silent or emit something distinct
- [x] 7.7 No new `.jpg` / `.png` files appear under `%LOCALAPPDATA%\Huddle\`; `scenarios.log` now contains blocks for both scenarios
