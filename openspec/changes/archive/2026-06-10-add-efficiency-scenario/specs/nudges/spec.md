## ADDED Requirements

### Requirement: Efficiency Insights scenario

The system SHALL ship a built-in scenario with key `efficiency-insights`, registered in `ScenarioRegistry.All` after `LearningsScenario`. The scenario SHALL run at 6-hour cadence with `TrailSize = 60` and `PriorNudgesSize = 10`, and SHALL declare `ModelId = Model.ClaudeOpus4_8`. Its display name SHALL be `EFFICIENCY` and its accent color `#6BA6FF`, distinct from the LinkedIn (`#C58BFF`), Achievements (`#54D2A6`), and Learnings (`#F5C56C`) colors. The scenario SHALL infer from the trail how the user currently works within dev workflow & tooling and surface ONE concrete, actionable efficiency improvement grounded in external best practice — a proven testing framework, a spec-driven-development practice, or a library/tool the user appears not to be using. When the research yields nothing above generic advice the user is likely already following, the scenario SHALL emit `{"emit": false, "reason": "..."}`. When it does emit, `title` SHALL be the improvement in one line, `body` SHALL be 1–2 sentences naming the proven better approach and why (with the source named in prose), and `sources` SHALL be the moment IDs from the trail that motivated the insight. The system prompt SHALL carve the boundary from the other scenarios: Achievements answers *what got done*, Learnings answers *how understanding changed*, Efficiency answers *how the user could work better based on external best practice*. The prompt SHALL instruct no emojis, no motivational framing, and hedge when ambiguous.

#### Scenario: Efficiency Insights is registered and runs on the tick

- **WHEN** the tick handler runs and the Efficiency Insights scenario is due
- **THEN** it is executed with a 60-moment trail and up to 10 prior Efficiency nudges as context

#### Scenario: Efficiency Insights throttles to once per 6 hours

- **WHEN** the Efficiency Insights scenario has run within the last 6 hours (in-memory `_lastRun`)
- **THEN** subsequent ticks do not run the scenario until 6 hours have elapsed

#### Scenario: Efficiency Insights call uses Opus 4.8

- **WHEN** the Efficiency Insights scenario runs (scheduled or manual)
- **THEN** both the research and synthesis Claude calls use the model `claude-opus-4-8`, and the `scenarios.log` block(s) record that model

#### Scenario: Dedup via prior nudges across runs

- **WHEN** a later run surfaces the same improvement that was already emitted
- **THEN** the scenario stays silent because the prior-nudges context names the already-emitted recommendation

#### Scenario: Display tag and dot

- **WHEN** an Efficiency Insights nudge renders
- **THEN** the card shows the tag `EFFICIENCY` and a cool-blue dot (`#6BA6FF`)

### Requirement: Web-research two-phase execution

The Efficiency Insights scenario SHALL gather external information using the web search server tool and SHALL produce its nudge in two phases, because web search results carry citations and structured JSON output is incompatible with citations.

In **phase 1 (research)**, the scenario SHALL call Claude with the web search tool (`WebSearchTool20260209`) enabled and no structured-output format, and SHALL let Claude issue searches — including community sources where they reflect real adoption — and summarize the findings as text. The web search tool SHALL be bounded by a `MaxUses` limit so the server completes its searches within a single response. The findings SHALL be the text the model writes in that response. If a research response returns with stop reason `pause_turn` (the server wanted more search rounds than its cap allowed), the scenario SHALL proceed to synthesis with the findings gathered so far rather than failing.

In **phase 2 (synthesis)**, the scenario SHALL make a second Claude call with **no tools** and the structured-output format `JsonOutputFormat` using `ScenarioPromptHelpers.BuildNudgeDraftSchema()`, passing phase 1's findings plus the trail context, and SHALL deserialize the result into a `NudgeDraft`. The resulting nudge SHALL be stored and rendered through the unchanged `NudgeStore` and `NudgeCard` path.

#### Scenario: Phase 1 enables web search without a JSON format

- **WHEN** the research call is made
- **THEN** the request includes the `WebSearchTool20260209` tool with a bounded `MaxUses` and does **not** set a `JsonOutputFormat`, so citations are permitted

#### Scenario: research is bounded and degrades gracefully

- **WHEN** the research response returns (whether `end_turn` or `pause_turn`)
- **THEN** the scenario extracts the model's text output as the findings and proceeds to synthesis, without crashing when the search loop was cut short by `MaxUses`

#### Scenario: Phase 2 produces structured output without tools

- **WHEN** the synthesis call is made with phase 1's findings
- **THEN** the request sets `JsonOutputFormat` from `ScenarioPromptHelpers.BuildNudgeDraftSchema()`, includes no tools, and the response deserializes into a `NudgeDraft`

#### Scenario: Storage and rendering are unchanged

- **WHEN** the synthesis phase emits a nudge
- **THEN** it is inserted via `NudgeStore` and rendered by the existing `NudgeCard` with no schema or UI change
