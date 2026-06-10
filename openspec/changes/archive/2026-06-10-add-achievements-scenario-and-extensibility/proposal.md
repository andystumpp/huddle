## Why

We're at the second-scenario moment. The LinkedIn Posts scenario shipped as a single static class with no abstraction, exactly per `CLAUDE.md`'s rule — *"don't introduce interfaces until a second implementation needs them."* Andy now wants an **Achievement tracker** that runs alongside LinkedIn and surfaces what they actually shipped today; that's the second implementation. Time to earn the abstraction, plug both scenarios into one orchestrator, and set up the shape future scenarios will use.

This change does three things together: introduces the `Scenario` base class + registry, refactors LinkedIn to inherit from it, and adds the Achievements scenario. As a useful side effect of having two scenarios sharing the same pipeline, we also wire up *previously-emitted nudges* as a per-scenario context block — so scenarios can avoid repeating themselves without us writing dedup logic by hand.

## What Changes

- Add `Scenario` abstract base class (`Key`, `Name`, `DisplayName`, `AccentColorHex`, `Cadence`, `TrailSize`, `PriorNudgesSize`, in-memory `_lastRun` + gate, `IsDue(now)`, public `RunAsync(trail, priorNudges, ct)` template, protected `ExecuteAsync` for the actual Claude call). Each scenario owns its own throttle clock.
- Add `Scenarios/ScenarioRegistry` — static `IReadOnlyList<Scenario> All` and `GetByKey(string)`. Hardcoded for now; the `.md` plugin loader can replace the All source in a later change.
- **Refactor** `LinkedInPostsScenario` to inherit from `Scenario`. Same behavior, same prompt, same throttle (1 hour). Now also receives prior LinkedIn nudges in context — the prompt's "don't repeat what you've already posted" clause finally has the data to back it up.
- **Add** `AchievementsScenario`:
  - Cadence: 1 hour.
  - TrailSize: 60 moments (~3 hours of work at the 3-min tick).
  - PriorNudgesSize: 20 (covers a full day's emissions for dedup).
  - System prompt asks for one specific, concrete achievement at a time (shipped / decided / resolved / learned / moved). Past tense for completed things, present for ongoing decisions. Plain voice, no emojis, no motivational framing. Emit silently `{emit:false}` (with `reason`) when nothing new has been achieved since the last emit.
  - The model receives previously-emitted achievement nudges in its context so it can de-duplicate without us writing dedup code.
- Extend `NudgeStore` with `RecentByScenarioAsync(string scenario, int limit)` for the per-scenario prior-nudges context.
- Update `PeekPanelWindow.OnSchedulerTick`: replace the hardcoded LinkedIn call with `foreach (var scenario in ScenarioRegistry.All) if (scenario.IsDue(now)) { ... }`. Each scenario gets its own trail (capped at its `TrailSize`) and its own prior-nudges (capped at its `PriorNudgesSize`).
- Update the manual trigger to **run every scenario** (bypassing throttle), aggregating the result into one status line: `Run complete: N emitted, M silent`.
- Update `NudgeCard` to read display name + accent color from `ScenarioRegistry.GetByKey(nudge.Scenario)` — no more hardcoded "LINKEDIN POSTS" / violet dot.

## Capabilities

### Modified Capabilities

- `nudges`:
  - The "Scenario runs on the moment-capture tick" requirement becomes plural — each enabled scenario evaluates per tick if due.
  - The "LinkedIn Posts scenario" requirement keeps its content but is rephrased as one of multiple "Built-in scenarios" alongside Achievements.
  - The "Manual scenario trigger" requirement now runs every scenario (one click, aggregated status).
  - The "Nudge card content" requirement gets the display name + accent color from the registry.
  - New: "Scenario abstraction" requirement covering the base class + registry pattern.
  - New: "Achievement tracker scenario" requirement covering cadence, trail, prompt voice, dedup via prior nudges.

## Impact

- New: `src/Huddle.App/Scenarios/Scenario.cs` — abstract base class.
- New: `src/Huddle.App/Scenarios/ScenarioRegistry.cs` — static list + lookup.
- New: `src/Huddle.App/Scenarios/AchievementsScenario.cs`.
- Modified: `src/Huddle.App/Scenarios/LinkedInPostsScenario.cs` — now inherits, takes prior nudges, emits Reason on silence (unchanged).
- Modified: `src/Huddle.App/Scenarios/NudgeDraft.cs` — unchanged on the wire; small refactor possible if the shared `ExecuteAsync` template needs a different signature.
- Modified: `src/Huddle.App/Storage/NudgeStore.cs` — adds `RecentByScenarioAsync`.
- Modified: `src/Huddle.App/Controls/NudgeCard.xaml(.cs)` — display name + accent color come from the registry; the violet hardcode goes away.
- Modified: `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — single tick handler iterates scenarios; manual trigger iterates all (no throttle), aggregates status.
- No DB schema change. The existing `nudges.scenario` column already carries the key.
- No new package.

## Cost note

Achievements at hourly cadence adds another Claude call per hour. Input is larger than LinkedIn (~5,600 tokens with the 60-moment trail + 20 prior nudges + system prompt), output is small. Roughly **$0.018 per call → ~$0.14/day** at 8 active hours, on top of LinkedIn's ~$0.10/day. Together that's ~$0.25/day in scenarios, on top of ~$1/day for the moment pipeline. The pause-on-lock change we just shipped covers a lot of the away-from-keyboard waste.
