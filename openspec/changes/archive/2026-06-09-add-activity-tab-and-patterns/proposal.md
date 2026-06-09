## Why

Before we wire any AI pipeline, we want to know what the *output* of Huddle looks like — not just nudges, but the **patterns** Huddle has noticed about your workday. The new design splits the panel into two tabs: **Nudges** (the suggestions, eventually) and **Activity** (the patterns that produced them). Activity-first matches Huddle's voice — "here's what I've been noticing" before "here's what I'd do about it" — and locks the pattern surface (and the data shape) before any detection lands. We seed it with fake patterns so we can iterate on the visual + interaction model in isolation.

Patterns are **observations**, not proposals. Scenarios (social ideas, efficiency, etc.) describe the *lens* through which a nudge is framed, not the underlying observation. A single pattern can fuel multiple nudges across different scenarios — so activity cards stay scenario-neutral, and the scenario tag belongs on the nudge card, not the pattern card.

## What Changes

- **BREAKING:** Remove the existing scenario filter chips (All / Social ideas / Efficiency). They're replaced by tabs.
- Add a two-tab strip directly under the header: **Nudges** and **Activity**, each with an inline count badge and a small glyph. Exactly one tab is selected at a time; **Activity** is the default for this change so the new surface is what the user sees first.
- Add the **Activity** tab content: a section header **"⊕ PATTERNS DETECTED N"** followed by a scrollable list of pattern cards.
- Add the **pattern card** visual — kept deliberately simple in this change: a bold one-line title, a one- or two-line description, and source-app monogram tiles. That's it.
- Seed four in-memory patterns matching the design — Heavy context-switching, Wrestling one sentence, Repeating yourself, and one more — so the tab renders against realistic content.
- Keep the **Nudges** tab visible but empty: clicking it switches to the existing empty-state ("No nudges right now."). Nudges content lands in a later change.
- Tab counts (Activity = pattern count, Nudges = nudge count) are computed from the in-memory data and update if the counts change.

## Capabilities

### Modified Capabilities
- `app-shell`: replace the scenario filter chips with a Nudges / Activity tab strip; add the Activity tab content (patterns-detected section + simple pattern cards) and the seeded pattern data shape. The empty state moves under the Nudges tab.

## Impact

- `src/Huddle.App/Views/PeekPanelWindow.xaml` — drop the filter-chip `StackPanel`; add a tab strip; add an Activity content area with a section header and an `ItemsRepeater` of pattern cards; the existing empty state is shown only when the Nudges tab is selected.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — add tab-selection state, expose the seeded pattern collection, expose the counts. Drop the chip-click handler.
- `src/Huddle.App/Controls/PatternCard.xaml(.cs)` — new user control for the pattern card.
- `src/Huddle.App/Controls/AppTile.xaml(.cs)` — new tiny user control for the source-app monogram.
- `src/Huddle.App/Models/Pattern.cs`, `src/Huddle.App/Models/PatternSeed.cs` — pattern record + seed data.
- `openspec/specs/app-shell/spec.md` — modified at archive time (filter chips → tabs, plus the new pattern requirements).
- No SQLite work, no pipeline work, no scenarios on patterns. Settings button remains a stub.
