## Why

Both scenarios currently hardcode `claude-sonnet-4-6`. That's the right default for Achievements (the trail is long, the body is short, Sonnet is fast and cheap) but the wrong floor for LinkedIn — post-quality matters disproportionately because one bad emit visibly burns trust, and the Opus tier is meaningfully better at the "find the sharp opinion in this noise" shape that LinkedIn posts ask for. Andy named the floor: **Opus 4.7 at least for LinkedIn, Sonnet stays for Achievements.**

The `Scenario` base class already owns visual identity, cadence, trail size, and prior-nudges size. Model is the same shape — per-scenario, declared on the class. Tiny refactor: add the property, override it per scenario, use it in the SDK call instead of the hardcode.

## What Changes

- Add `Model ModelId` (`Anthropic.Models.Messages.Model`) as a non-abstract virtual on `Scenario` with `Model.ClaudeSonnet4_6` as the default. Subclasses override when they want something else.
- `LinkedInPostsScenario` overrides to **`Model.ClaudeOpus4_8`** — the latest Opus, satisfies the "at least 4.7" floor and is the current recommended Opus per the `claude-api` skill.
- `AchievementsScenario` keeps the default (Sonnet 4.6) — no override needed.
- The Claude call inside each `ExecuteAsync` reads `ModelId` instead of the hardcoded `Model.ClaudeSonnet4_6`.
- `ScenarioDiagnostics.LogRun` takes the model identifier so `scenarios.log` shows which model actually ran (currently hardcoded to "claude-sonnet-4-6" in the header).

## Capabilities

### Modified Capabilities
- `nudges`: the scenario abstraction now declares per-scenario model selection; built-in scenarios specify their own.

## Impact

- Modify: `src/Huddle.App/Scenarios/Scenario.cs` — add `public virtual Model ModelId => Model.ClaudeSonnet4_6;`.
- Modify: `src/Huddle.App/Scenarios/LinkedInPostsScenario.cs` — override `ModelId => Model.ClaudeOpus4_8`. Reference `ModelId` (not the hardcode) in `MessageCreateParams`.
- Modify: `src/Huddle.App/Scenarios/AchievementsScenario.cs` — reference `ModelId` instead of the hardcode (no override needed).
- Modify: `src/Huddle.App/Scenarios/ScenarioDiagnostics.cs` — take the model identifier as a parameter and write it into the log header.
- No DB / store / UI change. No new package.

## Cost note

LinkedIn per-call cost roughly doubles: Sonnet 4.6 (~$3 / $15 per MT) → Opus 4.8 (~$5 / $25 per MT). At ~2,500 input + ~100 output tokens, that's ~$0.009 → ~$0.015 per call, ~$0.05/day added at the hourly cadence with the panel open all day. Achievements stays at the existing ~$0.014 per call.
