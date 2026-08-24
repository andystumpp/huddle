## Why

When the laptop is docked to (or undocked from) an external monitor, the peek panel ends up stranded in the middle of a screen and only a restart fixes it (issue #50). The panel computes its dock position once, at startup, and never reacts to display-configuration changes.

## What Changes

- The app watches for display-configuration changes and re-docks the panel automatically — no restart.
- A new `DisplayChangeWatcher` (mirroring the existing `SessionLockWatcher`) subclasses the panel window and raises a `Changed` event on `WM_DISPLAYCHANGE` (monitor add/remove, resolution change) and `WM_SETTINGCHANGE`/`SPI_SETWORKAREA` (work-area/taskbar change).
- `PeekPanelWindow` handles `Changed` with a short trailing-edge debounce (~400 ms, reset on each message so a burst of change messages collapses to one reposition) and calls the existing `PositionPanel()`, which already recomputes the whole dock + tab geometry from the primary display's work area and DPI.
- Target stays the **primary display** (`PositionPanel` already uses `MONITOR_DEFAULTTOPRIMARY`); no per-monitor tracking is introduced.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `app-shell`: the *Peek panel placement and chrome* requirement now recomputes the dock position on display-configuration changes, not only when the panel is shown.

## Impact

- `src/Huddle.App/Capture/DisplayChangeWatcher.cs` — new; window subclass raising `Changed`.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — construct/dispose the watcher next to `_lockWatcher`, plus a debounce `DispatcherTimer` that calls `PositionPanel()`.
- No change to the dock math itself; `PositionPanel()` is reused as-is.
