# app-shell delta — peek panel slide UX

## MODIFIED Requirements

### Requirement: Peek panel placement and chrome

The peek panel SHALL be a 384 px wide, borderless top-level window with no native title bar or caption buttons. It SHALL dock 12 px from the right edge of the primary display's work area and SHALL stretch vertically to the full work-area height minus a 12 px gap at the top and a 12 px gap at the bottom, with a 320 px minimum height. The panel window SHALL request DWM round corners (`DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`) and its content SHALL be clipped at an 8 px corner radius to match. Resize, minimize, and maximize SHALL be disabled. The panel SHALL recompute its dock position and slide geometry whenever it is shown. The panel SHALL remain always-on-top while open.

#### Scenario: Panel stretches the work-area height

- **WHEN** the app launches on a standard single-monitor setup
- **THEN** the panel's top edge sits 12 px below the work area's top edge, its bottom edge sits 12 px above the work area's bottom edge, and its right edge sits 12 px from the work area's right edge

#### Scenario: Panel has no native title bar

- **WHEN** the panel is visible
- **THEN** no native title bar, caption text, or caption buttons (minimize / maximize / close) are drawn at the top of the window

#### Scenario: Window corners are rounded

- **WHEN** the panel is visible
- **THEN** all four corners of the window — acrylic backdrop included — render rounded, with the content's 8 px clip tracing the window's corner curve

#### Scenario: Panel cannot be resized

- **WHEN** the user attempts to drag a window edge or click maximize / minimize
- **THEN** the window size does not change

## ADDED Requirements

### Requirement: Panel slide-out and slide-in

The panel SHALL hide itself by sliding fully off the right edge of the work area, and reveal itself by sliding back to its docked position. The panel SHALL slide out after the cursor has been outside the panel's visible bounds for 700 ms, and SHALL slide in when the cursor enters the peek chip's hover zone at the right screen edge. Each slide SHALL animate horizontally over 220 ms with an ease-out cubic curve, keeping the panel's vertical position fixed.

#### Scenario: Panel slides out when the cursor leaves

- **WHEN** the panel is visible and the cursor stays outside the panel's bounds for 700 ms
- **THEN** the panel animates off-screen to the right over 220 ms and is no longer visible in the work area

#### Scenario: Cursor returning within the grace period cancels the hide

- **WHEN** the panel is visible, the cursor leaves the panel, and returns within 700 ms
- **THEN** the panel stays visible and the grace timer restarts on the next leave

#### Scenario: Hovering the chip slides the panel in

- **WHEN** the panel is hidden and the cursor enters the peek chip's hover zone
- **THEN** the panel animates from off-screen to its docked position over 220 ms

### Requirement: Peek chip window

While the panel is hidden, the app SHALL show a separate always-on-top chip window at the right edge of the work area: 28 px of visible width, 168 px tall, vertically centered. The chip SHALL be shaped as a left-rounded tab via a window region whose right corners fall past the screen edge. Because the OS clamps top-level windows to a minimum track width (~133 px), the chip window SHALL anchor its content to the left 28 px — the on-screen portion — and let the clamped excess hang past the screen edge. The chip SHALL be hidden whenever the panel is visible: it disappears when a slide-in starts and appears when a slide-out completes.

#### Scenario: Chip appears when the panel finishes hiding

- **WHEN** the panel's slide-out animation completes
- **THEN** the chip is visible at the right edge of the work area, vertically centered, showing the unread count

#### Scenario: Chip disappears when the panel returns

- **WHEN** a slide-in begins
- **THEN** the chip window is hidden before the panel reaches its docked position

#### Scenario: Chip content sits in the on-screen sliver

- **WHEN** the chip is visible
- **THEN** the count renders within the leftmost 28 px of the chip window — the portion on screen — regardless of the actual clamped window width

### Requirement: Unread nudge count on the chip

The chip SHALL display the number of nudges that arrived while the panel was not "seen". A nudge increments the unread count when it is inserted and the panel has not been open for the read-grace period. The unread count SHALL reset to 0 — and the panel SHALL be marked seen — once the panel has stayed open for 3 seconds; sliding out before that preserves the count. While the unread count is greater than zero, the chip SHALL render a pulsing halo behind the number; at zero the halo is still.

#### Scenario: Nudges arriving while hidden increment the count

- **WHEN** the panel is hidden and a scenario emits two nudges
- **THEN** the chip shows "2" with a pulsing halo

#### Scenario: Count resets after the panel is open 3 seconds

- **WHEN** the panel slides in and stays open for 3 seconds
- **THEN** the unread count resets to 0 and the chip shows "0" with no pulse on the next slide-out

#### Scenario: A quick peek preserves the count

- **WHEN** the panel slides in and slides back out in under 3 seconds
- **THEN** the chip still shows the previous unread count
