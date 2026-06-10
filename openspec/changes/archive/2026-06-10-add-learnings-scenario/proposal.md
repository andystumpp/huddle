## Why

Huddle currently surfaces achievements and post drafts, but the user has no
end-of-day pause to see what they actually *learned*. Achievements covers
"shipped / decided / resolved / learned / moved" in one lens at hourly
cadence; learnings get diluted by faster-moving things. A scenario that
runs once a day with a full-day trail and names only the concrete
learnings — a new pattern adopted, a gotcha discovered, a belief
updated, a heuristic refined — turns Huddle into something the user can
read back at the end of the day and recognize.

## What Changes

- Add a new scenario `LearningsScenario` (key `learnings`) that runs at
  24-hour cadence with `TrailSize = 200` (covers an ~10-hour workday at
  the 3-min capture cadence with headroom) and `PriorNudgesSize = 5`.
- Scenario uses `Model.ClaudeOpus4_8`, matching the LinkedIn scenario's
  reasoning-quality tier — picking out a real "learned" thread from a
  full day of moments is closer to the LinkedIn shape than the Sonnet
  Achievements shape.
- Register the scenario in `ScenarioRegistry.All` after `Achievements`.
- Tag display: `LEARNINGS`, accent color `#F5C56C` (warm amber), distinct
  from `#C58BFF` (LinkedIn) and `#54D2A6` (Achievements).
- The 24-hour throttle remains in-memory (consistent with the other
  scenarios). If the app restarts mid-day, the scenario re-fires; the
  existing `PriorNudgesSize` dedup mechanism keeps it from repeating
  itself within the day.

## Capabilities

### New Capabilities
<!-- No new capabilities — this is a new requirement under the existing nudges capability. -->

### Modified Capabilities
- `nudges`: adds a new Requirement for the Learnings scenario, parallel
  to the existing Achievements and LinkedIn Posts requirements.

## Impact

- New file: `src/Huddle.App/Scenarios/LearningsScenario.cs` —
  ~140 lines, mirrors `AchievementsScenario.cs` structure.
- Modified: `src/Huddle.App/Scenarios/ScenarioRegistry.cs` — one line
  appended to the `All` initializer.
- No DB schema change, no UI change, no new dependency. The
  `NudgeCard` already reads accent / display from
  `ScenarioRegistry.GetByKey(nudge.Scenario)`, so the warm-amber tag
  appears automatically.
- Cost: one extra Opus call per day, gated by the 24-hour throttle.
