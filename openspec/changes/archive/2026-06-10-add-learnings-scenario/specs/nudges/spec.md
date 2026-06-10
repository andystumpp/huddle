## ADDED Requirements

### Requirement: Learnings scenario

The system SHALL ship a built-in scenario with key `learnings`, registered in `ScenarioRegistry.All` after `AchievementsScenario`. The scenario SHALL run at 24-hour cadence with `TrailSize = 200` and `PriorNudgesSize = 5`, and SHALL declare `ModelId = Model.ClaudeOpus4_8`. Its system prompt SHALL ask Claude to identify ONE concrete learning from the day's trail — a new pattern adopted, a previous belief updated, a gotcha discovered, a heuristic refined, or a new tool / API / library learned — and SHALL explicitly carve the boundary from Achievements: Achievements answers *what got done*, Learnings answers *how understanding changed*. The prompt SHALL instruct plain past-tense second-person voice, anchored in concrete moments, no emojis, no motivational framing, hedge when ambiguous. When the day's trail shows no genuine learning thread, the scenario SHALL emit `{"emit": false, "reason": "..."}`. When it does emit, `title` SHALL be the learning in one line, `body` SHALL be 1–2 sentences of concrete context (what changed in their head and why), and `sources` SHALL be the moment IDs that show the learning.

#### Scenario: Learnings is registered and runs on the tick

- **WHEN** the tick handler runs and the Learnings scenario is due
- **THEN** it is executed with a 200-moment trail and up to 5 prior Learnings nudges as context

#### Scenario: Learnings throttles to once per 24 hours

- **WHEN** the Learnings scenario has run within the last 24 hours (in-memory `_lastRun`)
- **THEN** subsequent ticks do not run the scenario until 24 hours have elapsed

#### Scenario: Learnings call uses Opus 4.8

- **WHEN** the Learnings scenario runs (scheduled or manual)
- **THEN** the Claude call's `model` field is `claude-opus-4-8` and the `scenarios.log` block records that model

#### Scenario: Dedup via prior nudges across restarts

- **WHEN** the app restarts within the same day and the trail still reflects a learning that was emitted earlier that day
- **THEN** the Learnings scenario stays silent because the prior-nudges context names the already-emitted learning

#### Scenario: Display tag and dot

- **WHEN** a Learnings nudge renders
- **THEN** the card shows the tag `LEARNINGS` and a warm-amber dot (`#F5C56C`)
