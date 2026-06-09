# app-shell Specification

## Purpose
TBD - created by archiving change switch-shell-to-taskbar-window. Update Purpose after archive.
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

- **WHEN** the user closes the panel window (close button, Alt+F4, or taskbar context menu)
- **THEN** the Huddle process terminates and the taskbar entry disappears

### Requirement: Peek panel placement and chrome

The peek panel SHALL be a 384 px wide window with standard native title bar chrome and a close affordance, but with resize, minimize, and maximize disabled. It SHALL dock 12 px from the right edge and 12 px above the taskbar of the primary display's work area. Its height SHALL be capped at `workArea.Height - 84 px`. The panel SHALL have 13 px rounded corners on its client area and a DesktopAcrylic background. The panel SHALL recompute its dock position whenever it is shown. The panel SHALL remain always-on-top while open.

#### Scenario: Panel docks to bottom-right on launch

- **WHEN** the app launches on a standard single-monitor setup
- **THEN** the panel's right edge sits 12 px from the work area's right edge and its bottom edge sits 12 px above the work area's bottom edge

#### Scenario: Panel renders with acrylic and rounded corners

- **WHEN** the panel is visible on a system with transparency effects enabled
- **THEN** the client area is rendered with DesktopAcrylic material and its corners are rounded with a 13 px radius

#### Scenario: Panel does not auto-hide

- **WHEN** the panel is visible and the user activates another window
- **THEN** the panel stays visible — it does not hide on lost activation, and it does not hide on Esc

#### Scenario: Panel cannot be resized

- **WHEN** the user attempts to drag a window edge or click maximize / minimize
- **THEN** the window size does not change (resize, minimize, and maximize are disabled)

### Requirement: Placeholder brand content

The peek panel SHALL render a placeholder header containing the Huddle mark (the three overlapping discs from `design/huddle/project/huddle/panel.jsx` `HuddleMark`) and the text **"Huddle"**. The panel SHALL render no other content in this change — no status line, no pause control, no filter chips, no nudge stream, no settings button.

#### Scenario: Panel shows the brand block

- **WHEN** the panel is visible
- **THEN** the Huddle mark and the text "Huddle" are visible in the header area

#### Scenario: Panel shows nothing else

- **WHEN** the panel is visible
- **THEN** no pause button, no settings button, no status line, no filter chips, no look-bar, and no nudge cards are rendered

