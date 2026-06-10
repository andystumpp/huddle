## ADDED Requirements

### Requirement: Card relative timestamps

Cards in the peek panel SHALL display a relative timestamp derived from the record's `ts`. The label text SHALL be:

- **"just now"** when the age is under 60 seconds,
- **"Nmin ago"** for a whole-minute age from 1 to 59 minutes (e.g. `3min ago`),
- **"Nh ago"** for a whole-hour age from 1 to 23 hours (e.g. `2h ago`),
- **"Nd ago"** for a whole-day age of 1 day or more (e.g. `5d ago`).

The formatting SHALL be produced by a single shared helper used by every card type. The panel SHALL drive a single shared clock that ticks at least once per minute while the panel is open and refreshes every visible card's relative timestamp in place, without re-rendering or reordering the card lists. Cards SHALL subscribe to the clock when they are realized and unsubscribe when they leave the visual tree (the lists are virtualized).

#### Scenario: Fresh card reads "just now"

- **WHEN** a card's `ts` is less than 60 seconds before the current time
- **THEN** its relative timestamp reads "just now"

#### Scenario: Minute-old card reads "Nmin ago"

- **WHEN** a card's `ts` is 3 minutes before the current time
- **THEN** its relative timestamp reads "3min ago"

#### Scenario: Hour-old card reads "Nh ago"

- **WHEN** a card's `ts` is 2 hours before the current time
- **THEN** its relative timestamp reads "2h ago"

#### Scenario: Day-old card reads "Nd ago"

- **WHEN** a card's `ts` is 5 days before the current time
- **THEN** its relative timestamp reads "5d ago"

#### Scenario: Open panel refreshes labels over time

- **WHEN** the panel stays open and a card that read "just now" crosses the one-minute mark
- **THEN** the shared clock updates that card's label to "1min ago" in place, with no list re-render

## MODIFIED Requirements

### Requirement: Moment card content

Each moment card SHALL show the model's 1–2 sentence summary text as the main content, followed by a single footer row containing the source app's monogram tile (via `AppTile`), the foreground window title, and a relative timestamp derived from `moment.ts` (per the *Card relative timestamps* requirement) aligned to the row's right edge. The window title SHALL trim with character-ellipsis if it doesn't fit between the tile and the timestamp on a single line. Moments are scenario-neutral observations — no scenario tag, no scenario rail, no nudge badge.

#### Scenario: Card shows summary, source tile, title, and timestamp

- **WHEN** a moment card renders
- **THEN** the summary text is visible as the main body, and the footer shows one `AppTile`, the window title, and the relative timestamp

#### Scenario: Window title is trimmed when too long

- **WHEN** a moment's window title exceeds the available footer width on a single line
- **THEN** the title is trimmed with a trailing ellipsis (no wrapping to a second line), while the relative timestamp stays fully visible
