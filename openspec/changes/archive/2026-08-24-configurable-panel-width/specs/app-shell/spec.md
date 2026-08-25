## MODIFIED Requirements

### Requirement: Peek panel placement and chrome

The peek panel SHALL be a borderless top-level window with no native title bar or caption buttons, whose width is configurable via the top-level `panelWidth` setting (in DIPs), defaulting to 384 and clamped to a sane range so an out-of-range value cannot produce an unusable window. It SHALL dock 12 px from the right edge of the primary display's work area and SHALL stretch vertically to the full work-area height minus a 12 px gap at the top and a 12 px gap at the bottom, with a 320 px minimum height. The panel window SHALL request DWM round corners (`DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`) and its content SHALL be clipped at an 8 px corner radius to match. Resize, minimize, and maximize SHALL be disabled. The panel SHALL recompute its dock position and slide geometry whenever it is shown and whenever the display configuration changes — a monitor is added or removed, or the resolution, DPI, or work area changes — re-docking to the primary display's work area without requiring a restart. Display-change signals SHALL be coalesced so that a burst of changes results in a single reposition. The panel SHALL remain always-on-top while open.

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

#### Scenario: Panel re-docks when the display configuration changes

- **WHEN** a monitor is added or removed, or the resolution, DPI, or work area changes, while the app is running
- **THEN** the panel recomputes its dock position and returns to the primary display's work-area edge without a restart, coalescing a burst of change signals into a single reposition

#### Scenario: A configured width is applied

- **WHEN** `panelWidth` is set to a value within the allowed range
- **THEN** the panel docks at that width, still 12 px from the work area's right edge and stretching the work-area height; an out-of-range value is clamped into the range
