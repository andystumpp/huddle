## Why

The peek panel's Nudges tab was a flat, all-scenario, all-time stream of the most-recent 20 nudges. You could not review "today's achievements," scan a single day, or isolate one scenario across the week — everything was bundled together, which made the accumulated nudges hard to actually use. This is the first "Seeing" iteration: make the collected nudges reviewable before we build anything that turns them into content.

## What Changes

- The Nudges tab loads a **7-day window** of nudges (new `NudgeStore.SinceAsync(cutoff)`) instead of the flat most-recent 20.
- Nudges are **grouped under day headers** — `TODAY` / `YESTERDAY` / `MON · AUG 18` (local date), newest day first.
- A **scenario filter chip row** (`All · Achievements · Learnings · Posts · Efficiency`) isolates one scenario; single-select, `All` by default.
- Rendered by a heterogeneous display list (day-header rows + nudge cards) via a `DataTemplateSelector`; the `NudgeCard` is unchanged.
- Nudges tab only. The Activity/moments tab, `NudgeCard`, and all scenario/backend code are unchanged.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `app-shell`: Adds a day-grouped, scenario-filterable review to the Nudges tab (the tab strip and card rendering already live in this capability).

## Impact

- **Modified code**: `Storage/NudgeStore.cs` (`SinceAsync`), `Views/PeekPanelWindow.xaml` (filter chips, day-header + card templates, template selector), `Views/PeekPanelWindow.xaml.cs` (7-day load, filtered/grouped display rebuild, chip handler).
- **New code**: `Views/NudgeListItem.cs` (`NudgeDayHeader` record + `NudgeListItemTemplateSelector`).
- **Unchanged**: Activity/moments tab, `NudgeCard`, `MomentCard`, scenarios, backends, storage schema.
- **Deferred (out of scope)**: export / copy-to-markdown — that is the bridge to the separate "content pipeline" work and will be its own iteration.
