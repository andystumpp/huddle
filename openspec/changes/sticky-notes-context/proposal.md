## Why

Huddle's static `context` config lets the user write who they are / what they're working on once, and it's prepended to every scenario. But a work plan changes through the day, and re-editing config is friction. Windows **Sticky Notes** are where many people keep their live work plan and reminders — reading them gives scenarios fresh, dynamic context about what the user is currently doing, with zero extra typing.

This is purely context enrichment — the live sibling of the static `context`. It is not commitment tracking (#46) and not scenario selection (#55).

## What Changes

- An opt-in top-level config flag `stickyNotesContext` (boolean, default **false**).
- When enabled, Huddle reads the user's Sticky Notes from the packaged app's SQLite store (`SELECT text FROM note`), read-only, and injects the note text into each scenario's system prompt as **dynamic context** — after the static `context`, before the learned `profile`: `context + sticky-notes + profile + scenario prompt`. Read fresh each run so it always reflects the current notes.
- Fully graceful: with the flag off, an absent database, or any read error, nothing is injected (the scenario prompt is exactly as it is today). Default off preserves current behavior for everyone.

## Capabilities

### New Capabilities

- `sticky-notes-context`: opt-in reading of Windows Sticky Notes and injection of their text as dynamic context into scenario prompts.

### Modified Capabilities

<!-- none — the injection reuses the existing scenario prompt composition seam -->

## Impact

- `src/Huddle.App/Config/HuddleConfig.cs` — add `StickyNotesContext` (default false) parsed from the top-level `stickyNotesContext`.
- `src/Huddle.App/Memory/StickyNotesContext.cs` (new) — opt-in read of `%LOCALAPPDATA%\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite` (`SELECT text FROM note`), read-only, returns a formatted block or empty; all failures → empty.
- `src/Huddle.App/Scenarios/ConfiguredScenario.cs` — inject the sticky-notes block between the static context and the profile.
- `huddle.config.example.json` — document the flag (commented, off).
- Legacy pre-1607 `.snt` format is out of scope (SQLite path only).
