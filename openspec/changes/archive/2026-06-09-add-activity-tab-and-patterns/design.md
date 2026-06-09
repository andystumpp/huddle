## Context

The shell now matches the prototype's chrome: borderless acrylic, aurora sheen, header with watch-dot + countdown + pause, look-bar, filter chips, empty state. The next design from Andy reorganizes the content surface into two tabs — **Nudges** and **Activity** — and introduces a new object: **patterns**. Patterns are observations Huddle has surfaced from your workday ("Heavy context-switching", "Wrestling one sentence"); each one is the raw notice, agnostic to which scenario lens might later make use of it.

The Activity tab gives us a way to show Huddle's intelligence even before any nudges fire. It also lets us validate the data shape and visual model for patterns (title, description, sources, recency) before we commit to a detection pipeline.

This change is shell-only: no SQLite, no AI calls. Four hand-written patterns are loaded from a seed file at startup and rendered as cards. The Nudges tab is wired up but shows the existing empty state — populating it lands later.

**Patterns are scenario-neutral.** The decision that a context-switching pattern is best framed as an *efficiency* nudge happens later, in the scenario layer. Activity cards therefore carry no scenario tag, no scenario color, and no "N nudge(s)" badge — those concerns belong on the nudge card.

## Goals / Non-Goals

**Goals:**

- Replace the existing filter chips with a Nudges / Activity tab strip.
- Activity is the default selected tab so the new surface is what launches.
- Tabs show a glyph + label + numeric count; the selected tab has a clearly stronger visual (border accent, brighter background, opaque label).
- Activity tab content: a "⊕ PATTERNS DETECTED N" section header, then a scrollable column of pattern cards.
- Pattern card content (simple): bold one-line title, a description sentence, and source-app monogram tiles. Nothing else this iteration.
- Switching tabs is instant — no animation; the surface below the tabs swaps.
- Counts on each tab reflect the in-memory data (4 patterns, 0 nudges in this change).
- Seeded patterns load on startup from a static `PatternSeed` class.

**Non-Goals:**

- No SQLite, no persistence — patterns are in-memory for this change.
- No detection logic — patterns are hand-written seed data.
- No scenario on the pattern model or card — scenarios are a nudge-time concern.
- No "N nudge(s) →" badge on pattern cards.
- No strength glyph / signal bars.
- No filtering within the Activity tab.
- No animations / fresh-arrival effects on pattern cards.
- Nudges tab content stays as the existing empty state.
- No new icons in the title-bar area; brand block + status line are unchanged.

## Decisions

### D1. Custom two-button tab strip, not `Pivot` / `TabView`

- **Choice:** Two side-by-side `ToggleButton` controls in a `Grid` with two equal columns. State managed in code-behind; clicking either flips both `IsChecked` values and swaps the visible content panel below.
- **Rationale:** `Pivot` is touch-first and brings header swipe / animation behaviour we don't want. `TabView` is for document tabs (close button, drag-tear-out). The design here is two persistent, equal-width pills — the simplest control that fits.
- **Alternative considered:** `RadioButtons` — closer semantically but the default style is hard to bend to match the chunky pill look.

### D2. Pattern as an immutable record with a static seed

- **Choice:** A `record Pattern(string Id, string Title, string Description, IReadOnlyList<string> SourceApps)` exposed via `PatternSeed.All` (a `static readonly IReadOnlyList<Pattern>`).
- **Rationale:** Four fields cover this iteration's card. No mutation, no persistence yet. A static seed keeps the data model explicit and survives a future swap to a real source (replace `PatternSeed.All` with a store read).
- **Deliberately omitted (likely back in later iterations):** scenario (nudge-time concern, not observation-time), strength / signal bars, last-seen timestamp, derived nudge count. Each is one line to add when the iteration calls for it.
- **Source apps:** list of strings matching the existing `APP_META` keys from the prototype (`Code.exe`, `Chrome`, `Notepad`, `Slack`, `Windows Terminal`). Renderable directly via the `AppTile` control.

### D3. `AppTile` as a separate small user control

- **Choice:** Add `src/Huddle.App/Controls/AppTile.xaml(.cs)` rendering the 2-letter monogram in a tinted rounded square. Takes an `AppKey` (string) and a `Size` (int, default 22).
- **Rationale:** We need it again in the pattern footer (one or more tiles per card). Also reusable in the eventual nudge cards. Self-contained, no logic.
- **Monogram + tint table:** Hard-coded in the control, mirroring `data.jsx` `APP_META`. Keep simple — if it grows, move to a resource dictionary later.

### D4. Pattern card is a small UserControl, deliberately simple

- **Choice:** `src/Huddle.App/Controls/PatternCard.xaml(.cs)` is a `UserControl` with a `Pattern` dependency property. Layout: a single rounded card (no left rail), padded ~13 px, with a bold title, a description sentence below, and a footer row of source-app tiles. That's it.
- **Background / border:** the same subtle white-tint background and 1 px border the prototype nudge cards use (`rgba(255,255,255,0.045)` bg, `rgba(255,255,255,0.07)` border). Rounded 10 px corners.
- **Rationale:** We're locking the visual contract for *observations*. Anything decorative gets in the way of judging whether the title + description sentence + sources read as a useful notice.

### D5. Activity is the default tab in this change

- **Choice:** On launch, **Activity** is selected. (When nudge content lands later, the default may switch back to Nudges.)
- **Rationale:** The whole point of this change is the new surface; the user shouldn't have to click to see it.

### D6. Tab counts come from in-memory collections

- **Choice:** Nudges count = `0` (the existing empty state). Activity count = `PatternSeed.All.Count`. Both shown as plain numbers in the tab label.
- **Rationale:** Trivial today; once stores exist, they become observable counts.

### D7. Section header glyph: a "+" inside a circle

- **Choice:** Match the design's `⊕ PATTERNS DETECTED 4` exactly — small circled-plus glyph, all caps, letter-spaced label, count.
- **Rationale:** Anchors the surface; matches the design.

## Risks / Trade-offs

- **[ToggleButton group can fall out of "exactly one selected" if a user clicks the already-selected tab]** → Mitigation: on click, force the clicked tab to checked and the other to unchecked even if the click would have unchecked the active one (the existing chip handler already does this — same pattern).
- **[Removing scenario / strength / nudge count from the pattern model means we'll add columns back later]** → Accepted. Cheap to add a field; harder to scrub a premature one out of detection logic that grew around it. Lean small.
- **[Four seed patterns will go stale quickly — once we have real data the seed needs deleting, not just bypassing]** → Mitigation: gate the seed behind a single `PatternSeed.All` reference. When the real store lands, replace that reference with a store read in one place.
- **[`ItemsRepeater` doesn't virtualize as gracefully as `ListView` for very long lists]** → Not a concern at N=4. Revisit if Activity grows to dozens.

## Open Questions

- Should the Activity tab default change once Nudges has content? Current plan: yes, switch default back to Nudges in the change that introduces nudge content.
- Do we want a "Show older patterns" affordance below the seed cards? Out of scope here.
- Should pattern cards eventually expose a way to "see the nudges this pattern produced"? Likely yes, but it lives on the nudge side or on a hover affordance — not on the static card.
