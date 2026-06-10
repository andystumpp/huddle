## Why

The whole product bet from ADR 0001 is that *scenarios are prompts* — different markdown files that read recent moments and decide whether to emit a nudge. We've built the foundation: moments are observed, persisted, inferred for intent. The next slice plugs in the first real scenario and proves the second Claude call writes something worth reading.

We're starting with a single hardcoded scenario — **LinkedIn posts**, drafting principal-architect-voice post ideas from the user's actual work. It's voice-heavy (so a bad emit reads loud and we'll catch it fast), it's the user's chosen first scenario, and it doesn't need tools (no web search yet). The plugin-from-`.md` story, the trends scenario with `web_search`, and the dismiss / save UI are deferred to follow-up slices.

## What Changes

- Add a `nudges` SQLite table with columns matching the structured-output contract: `id`, `ts`, `scenario` (string key), `title`, `body`, `sources` (JSON array of moment IDs).
- Add `NudgeStore` (`AddAsync`, `RecentAsync(int limit)`, `CountAsync()`) — same shape and WAL-checkpoint pattern as `MomentStore`.
- Add `Scenarios/LinkedInPostsScenario` — hardcoded for this slice. Static class with cadence + trail size + a Claude text call using `output_config.format` for structured output.
- Wire the scenario into `PeekPanelWindow.OnSchedulerTick`: after the moment is persisted, check whether the scenario is due, and if so pull the trail (`MomentStore.RecentAsync(20)`), run the scenario, and persist the emitted nudge (if any).
- Add `Controls/NudgeCard` — title (semibold, primary), body (secondary text, wrapped), small scenario tag at the top.
- Replace the empty state in the Nudges tab's content area: when no nudges exist, the empty state stays; when nudges exist, an `ItemsRepeater` of `NudgeCard`s renders newest-first.
- Every emitted nudge is persisted, regardless of UI state. Dismiss / save UI is deferred; the underlying record is always kept.

## Capabilities

### New Capabilities
- `nudges`: scenarios that read recent moments and emit nudges, the SQLite store, and the panel rendering. Owns the scenario-runner orchestration and the nudge card visual.

### Modified Capabilities
_None. The Activity tab, the tick scheduler, the moment store, and the chrome are unchanged. The Nudges tab's existing empty state already covers the "no nudges yet" case in the `app-shell` spec; rendering nudges when they exist is the new `nudges` capability's concern._

## Impact

- New: `src/Huddle.App/Storage/Migrations/002_nudges.sql` — table + `idx_nudges_ts` index.
- New: `src/Huddle.App/Models/Nudge.cs` — record.
- New: `src/Huddle.App/Storage/NudgeStore.cs` — same three-method surface as `MomentStore`, with `PRAGMA wal_checkpoint(TRUNCATE);` after every insert.
- New: `src/Huddle.App/Scenarios/LinkedInPostsScenario.cs` — system prompt, structured-output schema, in-memory `s_lastRun`, `IsDue(now)` + `RunAsync(trail)`.
- New: `src/Huddle.App/Controls/NudgeCard.xaml(.cs)`.
- Modify: `src/Huddle.App/Views/PeekPanelWindow.xaml(.cs)` — add `_nudges` `ObservableCollection<Nudge>`; populate on load from `NudgeStore.RecentAsync(20)`; run the scenario in `OnSchedulerTick` after the moment lands; replace the empty-state-only Nudges content with a conditional render (empty state vs `NudgesRepeater`); wire `CountNudges` to `_nudges.Count`.
- Modify: `src/Huddle.App/Huddle.App.csproj` — no new packages; the existing `Microsoft.Data.Sqlite` and `Anthropic` cover everything.

## Cost note

One additional Sonnet 4.6 call per hour of panel-open time, with ~2,500 input tokens (system prompt + 20 moment summaries) and ~250 output tokens. Roughly **$0.012 per scenario call** ≈ $0.012/hour added on top of the existing $0.16/hour moment-pipeline cost. Cap on cost will come from per-scenario cadence, which is what we're proving here.
