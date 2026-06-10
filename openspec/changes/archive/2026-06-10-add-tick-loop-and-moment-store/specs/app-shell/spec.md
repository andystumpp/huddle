## MODIFIED Requirements

### Requirement: Look-bar progress hairline

The peek panel SHALL display a 2 px progress hairline at its top edge that fills horizontally as the next-look countdown advances. The countdown period SHALL be 180 seconds (3 minutes), matching the capture tick. When the countdown reaches zero, the hairline SHALL reset to empty and begin filling again. When the app is paused, the hairline SHALL be empty and stationary.

#### Scenario: Hairline fills over the 3-minute tick

- **WHEN** the watching countdown advances from 180 s to 0
- **THEN** the look-bar's filled width goes from 0% to 100% over that span

#### Scenario: Pause clears the hairline

- **WHEN** the app is paused
- **THEN** the look-bar is empty (0% width) and not animating

## REMOVED Requirements

### Requirement: Activity tab content — patterns detected

**Reason**: The Activity tab now renders real moments from the SQLite store, not seeded patterns. Replaced by "Activity tab content — observations".

**Migration**: None — the seed was always in-memory.

### Requirement: Pattern card content

**Reason**: Replaced by `MomentCard` rendering `Moment` records. The new card simplifies content to summary + source-app tile + window title.

**Migration**: None — `PatternCard` is deleted; the data type changed from `Pattern` to `Moment`.

### Requirement: Seeded pattern data

**Reason**: Replaced by real moments loaded from `MomentStore.RecentAsync(20)`. The four-pattern seed served its purpose locking the visual contract; the Activity tab is now backed by the store.

**Migration**: None — the seed never persisted.

## ADDED Requirements

### Requirement: Activity tab content — observations

When the Activity tab is selected, the content area SHALL display a section header reading **"OBSERVATIONS N"** in uppercase with a small circled-plus glyph to its left, where N is the number of moments currently rendered. Below the header, the panel SHALL render a vertically scrollable list of moment cards, ordered newest-first. The rendered list SHALL be capped at the 20 most recent moments; older moments stay in the store but are not displayed in this change.

#### Scenario: Section header shows the count

- **WHEN** the Activity tab is selected and N moments are loaded
- **THEN** the section header reads "OBSERVATIONS N" (uppercase) with a circled-plus glyph to its left

#### Scenario: Moments listed newest first

- **WHEN** the panel has loaded multiple moments
- **THEN** they render top-to-bottom from largest `ts` to smallest

#### Scenario: New moments appear at the top in real time

- **WHEN** the tick completes a successful capture while the panel is open
- **THEN** the new moment is inserted at position 0 of the visible list without restarting the app

#### Scenario: Older moments fall off the visible list

- **WHEN** the panel already shows 20 moments and a new one arrives
- **THEN** the new moment is shown at the top and the oldest is removed from the visible list (it remains in the store)

### Requirement: Moment card content

Each moment card SHALL show the model's 1–2 sentence summary text as the main content, followed by a single footer row containing the source app's monogram tile (via `AppTile`) and the foreground window title. The window title SHALL trim with character-ellipsis if it doesn't fit on a single line. Moments are scenario-neutral observations — no scenario tag, no scenario rail, no nudge badge.

#### Scenario: Card shows summary, source tile, and title

- **WHEN** a moment card renders
- **THEN** the summary text is visible as the main body, and the footer shows one `AppTile` plus the window title

#### Scenario: Window title is trimmed when too long

- **WHEN** a moment's window title exceeds the card width on a single line
- **THEN** the title is trimmed with a trailing ellipsis (no wrapping to a second line)
