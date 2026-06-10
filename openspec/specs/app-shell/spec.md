# app-shell Specification

## Purpose

The visible surface of Huddle: a single docked peek-panel window that runs as a standard Windows desktop app and renders the panel's chrome, header, look-bar countdown, and scenario filters. Pipeline-fed nudge content (cards, stream) lands in a later capability; this one defines the surface those nudges will render into.
## Requirements
### Requirement: Window-based app lifecycle

The Huddle app SHALL run as a standard Windows desktop application with a single top-level window. Launching the executable SHALL show the peek panel window immediately and add a Huddle entry to the Windows taskbar. The app SHALL NOT register a system tray icon or tray context menu. Closing the panel window SHALL terminate the process.

#### Scenario: Launch shows the window

- **WHEN** the user launches `Huddle.exe`
- **THEN** the peek panel window is visible on the desktop, docked to the bottom-right of the primary display's work area, and a Huddle entry appears in the taskbar

#### Scenario: No tray icon

- **WHEN** the app is running
- **THEN** no Huddle icon appears in the Windows system tray

#### Scenario: Close exits the app

- **WHEN** the user closes the panel window (Alt+F4, taskbar context menu)
- **THEN** the Huddle process terminates and the taskbar entry disappears

### Requirement: Peek panel placement and chrome

The peek panel SHALL be a 384 px wide, borderless top-level window with no native title bar or caption buttons. It SHALL dock 12 px from the right edge and 12 px above the taskbar of the primary display's work area. Its height SHALL be capped at `workArea.Height - 84 px`. The panel SHALL have 13 px rounded corners. Resize, minimize, and maximize SHALL be disabled. The panel SHALL recompute its dock position whenever it is shown. The panel SHALL remain always-on-top while open.

#### Scenario: Panel docks to bottom-right on launch

- **WHEN** the app launches on a standard single-monitor setup
- **THEN** the panel's right edge sits 12 px from the work area's right edge and its bottom edge sits 12 px above the work area's bottom edge

#### Scenario: Panel has no native title bar

- **WHEN** the panel is visible
- **THEN** no native title bar, caption text, or caption buttons (minimize / maximize / close) are drawn at the top of the window

#### Scenario: Panel does not auto-hide

- **WHEN** the panel is visible and the user activates another window
- **THEN** the panel stays visible — it does not hide on lost activation, and it does not hide on Esc

#### Scenario: Panel cannot be resized

- **WHEN** the user attempts to drag a window edge or click maximize / minimize
- **THEN** the window size does not change

### Requirement: Panel background and aurora sheen

The peek panel SHALL use a DesktopAcrylic system backdrop (or, on systems where acrylic is unavailable, a translucent dark fallback tint). The panel SHALL render two soft radial gradient overlays above the backdrop — a warm coral / pink wash anchored to the top-right corner, and a cool violet wash anchored to the bottom-left — matching the prototype's `data-style="aurora"` `::before` sheen. The panel SHALL use the Dark element theme regardless of the system's theme setting.

#### Scenario: Acrylic backdrop

- **WHEN** the panel is visible on a system with transparency effects enabled
- **THEN** the backdrop renders with DesktopAcrylic material

#### Scenario: Aurora sheen

- **WHEN** the panel is visible
- **THEN** a soft coral/pink radial wash is visible near the top-right corner, and a soft violet radial wash is visible near the bottom-left corner, both layered above the acrylic

#### Scenario: Dark theme is forced

- **WHEN** the user's Windows theme is set to Light
- **THEN** the panel still renders with dark-theme colors (light text on dark backdrop)

### Requirement: Look-bar progress hairline

The peek panel SHALL display a 2 px progress hairline at its top edge that fills horizontally as the next-look countdown advances. The countdown period SHALL be 180 seconds (3 minutes), matching the capture tick. When the countdown reaches zero, the hairline SHALL reset to empty and begin filling again. When the app is paused, the hairline SHALL be empty and stationary.

#### Scenario: Hairline fills over the 3-minute tick

- **WHEN** the watching countdown advances from 180 s to 0
- **THEN** the look-bar's filled width goes from 0% to 100% over that span

#### Scenario: Pause clears the hairline

- **WHEN** the app is paused
- **THEN** the look-bar is empty (0% width) and not animating

### Requirement: Panel header — brand, status, and controls

The peek panel SHALL display a header containing the Huddle mark, the brand name "Huddle", a status line, a pause/resume button, and a settings button. When the app is watching, the status line SHALL show a green pulsing dot, the text **"Watching · next look in M:SS"**, and the countdown SHALL match the look-bar. When the app is paused, the status line SHALL show **"Paused · not watching"** without the pulsing dot, and the look-bar SHALL stop advancing. The pause button SHALL toggle between watching and paused states, swapping its icon between pause and play glyphs. The settings button SHALL be present but MAY be a no-op stub.

#### Scenario: Watching shows a live countdown

- **WHEN** the app is in the watching state
- **THEN** the status line reads "Watching · next look in M:SS" and the seconds value decrements once per second

#### Scenario: Watch-dot pulses

- **WHEN** the app is in the watching state
- **THEN** a green dot is shown next to the status text with a slow pulsing halo around it

#### Scenario: Pause stops the countdown

- **WHEN** the user clicks the pause button while watching
- **THEN** the status line changes to "Paused · not watching", the pulsing dot disappears, the look-bar drops to 0%, and the pause icon swaps to a play icon

#### Scenario: Resume restarts the countdown

- **WHEN** the user clicks the play button while paused
- **THEN** the status line returns to "Watching · next look in M:SS", the look-bar resumes from zero, and the icon swaps back to pause

### Requirement: Empty state

When there are no nudges to show (either none exist, or none match the current filter), the panel SHALL display a centered empty state below the filter chips, containing a small spark glyph, the message **"No {scenario} nudges right now."** (or **"No nudges right now."** when the All filter is active), and the subtitle **"Huddle is watching — something useful will surface soon."**

#### Scenario: Empty state on first launch

- **WHEN** the panel is launched and the nudge store is empty
- **THEN** the empty state spark glyph, the "No nudges right now." line, and the watching subtitle are visible

#### Scenario: Empty state subtitle wording

- **WHEN** the empty state is visible
- **THEN** the subtitle reads "Huddle is watching — something useful will surface soon."

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

