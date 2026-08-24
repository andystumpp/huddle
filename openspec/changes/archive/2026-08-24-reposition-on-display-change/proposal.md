## Why

When the laptop is docked to (or undocked from) an external monitor, the peek panel ends up stranded in the middle of a screen and only a restart fixes it (issue #50). The panel computes its dock position once, at startup, and never reacts to display-configuration changes.

## What Changes

- The app watches for display-configuration changes and re-docks the panel automatically — no restart.
- A new `DisplayChangeWatcher` (mirroring the existing `SessionLockWatcher`) subclasses the panel window and raises a `Changed` event on `WM_DISPLAYCHANGE` (monitor add/remove, resolution change) and `WM_SETTINGCHANGE`/`SPI_SETWORKAREA` (work-area/taskbar change).
- `PeekPanelWindow` handles `Changed` with a trailing-edge debounce (~600 ms, reset on each message so a burst collapses to one reposition), then runs `PositionPanel()` twice a short spacing apart — a settle pass and a confirmation pass — because a dock's new primary and work area can finalize a beat after the last message.
- `PositionPanel()` recomputes the whole dock + tab geometry from the primary display's work area and DPI. Its work-area lookup is corrected to resolve the **true primary** via `MonitorFromPoint((0,0), MONITOR_DEFAULTTOPRIMARY)` — previously it used `MonitorFromWindow(panel)`, which returns whatever monitor the panel's rectangle currently overlaps (the flag is only an off-screen fallback). That made the dock target — and the computed height — depend on where the panel happened to sit, so after a display switch it read the wrong, shorter work area (visible as a panel that comes in short, and as a height that differs between the parked and revealed states).
- Target is the **primary display**; no per-monitor tracking is introduced.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `app-shell`: the *Peek panel placement and chrome* requirement now recomputes the dock position on display-configuration changes, not only when the panel is shown.

## Impact

- `src/Huddle.App/Capture/DisplayChangeWatcher.cs` — new; window subclass raising `Changed`.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — construct/dispose the watcher next to `_lockWatcher`, plus a debounce `DispatcherTimer` that calls `PositionPanel()`; and `TryGetPrimaryWorkArea` now resolves the primary via `MonitorFromPoint((0,0))` instead of `MonitorFromWindow(panel)`.
- The dock layout math (gaps, min-height, slide geometry) is otherwise unchanged; `PositionPanel()` is reused as-is.
