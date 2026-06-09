## REMOVED Requirements

### Requirement: Scenario filter chips

**Reason**: Replaced by the Nudges / Activity tab strip. The All / Social / Efficiency filter was a per-stream lens; the new design organizes the panel surface around two top-level views (the suggestions Huddle made and the patterns it observed). Filtering may return inside individual tabs later, but not as a top-level control.

**Migration**: None — the chips never shipped real filtering against persisted data. Any future "filter by scenario" need is met by per-tab affordances.

## ADDED Requirements

### Requirement: Nudges / Activity tab strip

The peek panel SHALL display a two-tab strip directly below the panel header, replacing the previous filter chips. The tabs SHALL be **Nudges** (with a lightbulb glyph) and **Activity** (with a pulse / activity glyph). Each tab SHALL display its label and a numeric count badge — Nudges count = the number of visible nudges; Activity count = the number of patterns detected. Exactly one tab SHALL be selected at a time; the selected tab SHALL be rendered with a brighter background, a colored accent border on its left edge, and full-opacity label text, while the unselected tab uses the muted chip surface. On first launch, the **Activity** tab SHALL be selected by default.

#### Scenario: Both tabs are visible

- **WHEN** the panel is visible
- **THEN** the Nudges tab and the Activity tab are both visible below the header, side by side, each showing a glyph, a label, and a count

#### Scenario: Activity is the default

- **WHEN** the panel is launched
- **THEN** the Activity tab is selected and its content (the patterns-detected section) is shown below

#### Scenario: Selecting Nudges switches the surface

- **WHEN** the user clicks the Nudges tab
- **THEN** the Nudges tab becomes selected, the Activity tab is deselected, and the content area swaps to the nudges empty state

#### Scenario: Selecting an already-selected tab keeps it selected

- **WHEN** the user clicks the currently-selected tab
- **THEN** that tab stays selected (it does not toggle off)

#### Scenario: Tab counts reflect the data

- **WHEN** the panel loads with N patterns and 0 nudges
- **THEN** the Activity tab shows the count "N" and the Nudges tab shows "0"

### Requirement: Activity tab content — patterns detected

When the Activity tab is selected, the content area SHALL display a section header reading **"PATTERNS DETECTED N"** in uppercase with a small circled-plus glyph to its left, where N is the number of patterns currently shown. Below the header, the panel SHALL render a vertically scrollable list of pattern cards in the order provided by the pattern source.

#### Scenario: Section header shows the count

- **WHEN** the Activity tab is selected and 4 patterns are loaded
- **THEN** the section header reads "PATTERNS DETECTED 4" (uppercase) with a circled-plus glyph to its left

### Requirement: Pattern card content

Each pattern card SHALL show a bold one-line title, a one- or two-line description sentence, and a row of source-app monogram tiles (one per app in `sourceApps`). Patterns are scenario-neutral observations — scenario framing belongs on the nudge card, not the pattern card.

#### Scenario: Card shows title, description, and sources

- **WHEN** a pattern card renders
- **THEN** all three elements are visible: title, description, and source-app tile(s)

### Requirement: Seeded pattern data

The panel SHALL be backed by a static, in-memory seed of four patterns loaded on startup, with no persistence in this change. The seed SHALL include at least: a "Heavy context-switching" pattern (sources VS Code + Chrome), a "Wrestling one sentence" pattern (source Code.exe), a "Repeating yourself" pattern (sources Code.exe + Slack), and one additional pattern for visual completeness below the fold.

#### Scenario: Four seed patterns load on startup

- **WHEN** the panel launches
- **THEN** exactly four pattern cards are rendered on the Activity tab, matching the seeded patterns
