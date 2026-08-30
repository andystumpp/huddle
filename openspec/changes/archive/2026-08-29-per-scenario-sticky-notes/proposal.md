## Why

Sticky-notes injection is currently a single top-level flag (`stickyNotesContext`) — all-or-nothing across every scenario. That's blunt: your live notes are useful context for some scenarios (e.g. one reasoning about your current plan) but noise for others. Per-scenario opt-in lets each scenario decide, matching how `model`, `effort`, and `agent` already work per scenario.

## What Changes

- Replace the top-level `stickyNotesContext` flag with a per-scenario `stickyNotes` boolean (default false). Only scenarios that set `stickyNotes: true` get the notes injected into their prompt.
- The read logic (read-only, deleted-filtered, `\id=` stripped, newest-first, no cap) and the injection position (`context` + sticky-notes + `profile` + scenario prompt) are unchanged — only the *gate* moves from global to per-scenario.
- The top-level `stickyNotesContext` flag is removed. It was opt-in and default off, so no scenario changes behavior unless it sets `stickyNotes` itself.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `sticky-notes-context`: the opt-in is now per scenario (`stickyNotes` on the scenario) rather than a single top-level flag; only opted-in scenarios read and inject the notes.
- `scenario-config`: the inline scenario definition gains an optional `stickyNotes` field.

## Impact

- `src/Huddle.App/Config/ScenarioConfig.cs` + `HuddleConfig.cs` — add per-scenario `StickyNotes` (default false); remove the top-level `StickyNotesContext` property and its parse.
- `src/Huddle.App/Memory/StickyNotesContext.cs` — drop the global-flag guard from `Read()`; it now just reads (the caller gates).
- `src/Huddle.App/Scenarios/ConfiguredScenario.cs` — inject the sticky-notes block only when `_def.StickyNotes` is true.
- `huddle.config.example.json` — move the doc from the top level to a per-scenario `stickyNotes` example.
