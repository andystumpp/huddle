## 1. Base class

- [x] 1.1 In `src/Huddle.App/Scenarios/Scenario.cs`, add `public virtual Model ModelId => Model.ClaudeSonnet4_6;` (requires `using Anthropic.Models.Messages;`)

## 2. Per-scenario overrides

- [x] 2.1 In `LinkedInPostsScenario`, override `ModelId => Model.ClaudeOpus4_8;`
- [x] 2.2 In `LinkedInPostsScenario.ExecuteAsync`, replace the hardcoded `Model = Model.ClaudeSonnet4_6` in `MessageCreateParams` with `Model = ModelId`
- [x] 2.3 In `AchievementsScenario.ExecuteAsync`, replace the hardcoded `Model = Model.ClaudeSonnet4_6` with `Model = ModelId` (no override needed — inherits the default Sonnet)

## 3. Diagnostic log

- [x] 3.1 Change `ScenarioDiagnostics.LogRun` signature to accept the model identifier (e.g., `string modelLabel`) and write it into the header line instead of the hardcoded `model=claude-sonnet-4-6`
- [x] 3.2 Update both scenarios' `ExecuteAsync` calls to pass `ModelId.Value` (or `.ToString()` if `Value` isn't available) as the new parameter

## 4. Verification

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 4.2 Trigger both scenarios via the manual play button. `scenarios.log` shows distinct model headers per run — `model=claude-opus-4-8` for the LinkedIn block and `model=claude-sonnet-4-6` for the Achievements block
- [x] 4.3 LinkedIn emits read in a similar voice but with a slightly sharper take than before (subjective; ideally compared against a prior emit)
- [x] 4.4 No regressions in Achievements behavior
