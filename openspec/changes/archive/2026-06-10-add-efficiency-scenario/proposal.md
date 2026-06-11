## Why

Huddle's three scenarios all reason *only* over the moment trail: Achievements
("what got done"), Learnings ("how understanding changed"), LinkedIn ("what's
post-worthy"). None of them can tell the user about something that isn't already
on their screen. But a lot of efficiency loss is invisible from the inside — you
keep fixing the same class of bug by hand because you don't know a testing
framework would catch it, or you iterate manually where spec-driven development
is proven to help. The user explicitly wants a scenario that looks at *how* they
work and researches the **latest external best practices** that apply — including
community sources like Reddit — to surface "you do X this way; the proven better
way is Y." That requires the web, which no current scenario touches.

## What Changes

- Add a new scenario `EfficiencyInsightsScenario` (key `efficiency-insights`)
  running at **6-hour cadence** with `TrailSize = 60` and `PriorNudgesSize = 10`,
  using `Model.ClaudeOpus4_8`.
- **First scenario to use a server-side tool.** It enables the web search tool
  (`WebSearchTool20260209`) so Claude can research the current landscape of dev
  workflow & tooling — spec-driven development, testing frameworks for agentic
  development, libraries/tools the user is underusing — and cite real sources,
  including Reddit/community threads.
- **Two-phase execution.** Web search produces citations, which are incompatible
  with structured JSON output. So phase 1 runs an agentic web-search loop
  (handling `pause_turn`) to gather findings; phase 2 makes a second Claude call
  with no tools that turns the findings + trail into the existing `NudgeDraft`
  JSON. Storage and the `NudgeCard` are unchanged.
- Scope is **dev workflow & tooling** — strictly how the user builds software.
- Register the scenario in `ScenarioRegistry.All` after `LearningsScenario`.
- Tag display: `EFFICIENCY`, accent color `#6BA6FF` (cool blue), distinct from
  `#C58BFF` (LinkedIn), `#54D2A6` (Achievements), `#F5C56C` (Learnings).

## Capabilities

### New Capabilities
<!-- No new capability — this is a new requirement under the existing nudges capability. -->

### Modified Capabilities
- `nudges`: adds Requirements for (1) the Efficiency Insights scenario and (2)
  the web-research two-phase execution model it introduces — parallel to the
  existing Achievements / Learnings / LinkedIn Posts requirements, but the first
  scenario that calls a server-side tool and runs more than one Claude call.

## Impact

- New file: `src/Huddle.App/Scenarios/EfficiencyInsightsScenario.cs` — larger
  than the other scenarios (~200 lines) because of the web-search loop and the
  second synthesis call.
- Modified: `src/Huddle.App/Scenarios/ScenarioRegistry.cs` — one line appended
  to the `All` initializer.
- No DB schema change, no UI change. The `NudgeCard` already reads accent /
  display from `ScenarioRegistry.GetByKey(nudge.Scenario)`, so the blue
  `EFFICIENCY` tag appears automatically.
- Dependency: uses the web search server tool already available in the
  `Anthropic` SDK (`WebSearchTool20260209`) — no new NuGet package.
- Cost: one web-research Opus call (multiple internal search rounds) plus one
  synthesis Opus call, every 6 hours, gated by the cadence throttle. Materially
  more expensive per run than the trail-only scenarios — the 6-hour cadence
  bounds it.
- Network: this is the first scenario that makes outbound web requests (via
  Anthropic's server-side search). The existing "pause when screen locked" gate
  already prevents runs while the user is away.
