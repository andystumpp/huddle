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

The peek panel SHALL display a 2 px progress hairline at its top edge that fills horizontally as the next-look countdown advances. When the countdown reaches zero, the hairline SHALL reset to empty and begin filling again. When the app is paused, the hairline SHALL be empty and stationary.

#### Scenario: Hairline fills over the tick

- **WHEN** the watching countdown advances from start to finish
- **THEN** the look-bar's filled width goes from 0% to 100%

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

### Requirement: Scenario filter chips

The peek panel SHALL display three filter chips below the header: **All**, **Social ideas**, **Efficiency**. Each chip SHALL show the count of non-dismissed nudges matching its scenario (zero while no nudges exist). Selecting a chip SHALL filter the stream to only show nudges of that scenario; **All** SHALL show every non-dismissed nudge. Exactly one chip SHALL be selected at a time, defaulting to **All** on launch. The two scenario chips SHALL include a small colored dot — violet for Social, teal for Efficiency.

#### Scenario: Default filter is All

- **WHEN** the panel opens for the first time
- **THEN** the **All** chip is selected, both other chips are deselected, and every non-dismissed nudge would be visible in the stream

#### Scenario: Selecting a chip filters the stream

- **WHEN** the user selects the **Social ideas** chip
- **THEN** the stream shows only nudges with scenario `social`, and the **Social ideas** chip is the only one in the selected state

### Requirement: Empty state

When there are no nudges to show (either none exist, or none match the current filter), the panel SHALL display a centered empty state below the filter chips, containing a small spark glyph, the message **"No {scenario} nudges right now."** (or **"No nudges right now."** when the All filter is active), and the subtitle **"Huddle is watching — something useful will surface soon."**

#### Scenario: Empty state on first launch

- **WHEN** the panel is launched and the nudge store is empty
- **THEN** the empty state spark glyph, the "No nudges right now." line, and the watching subtitle are visible

#### Scenario: Empty state subtitle wording

- **WHEN** the empty state is visible
- **THEN** the subtitle reads "Huddle is watching — something useful will surface soon."
