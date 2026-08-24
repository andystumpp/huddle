## 1. Display-change watcher

- [x] 1.1 Add `src/Huddle.App/Capture/DisplayChangeWatcher.cs`, mirroring `SessionLockWatcher`: subclass the given `hwnd` via `SetWindowSubclass` (delegate held in a field for GC-safety), raise `event EventHandler Changed` on `WM_DISPLAYCHANGE` (0x007E) and on `WM_SETTINGCHANGE` (0x001A) when `wParam == SPI_SETWORKAREA` (0x002F), always forwarding to `DefSubclassProc`. `Dispose()` removes the subclass.

## 2. Wire into the panel

- [x] 2.1 In `PeekPanelWindow`, construct `_displayWatcher = new DisplayChangeWatcher(_hwnd)` next to `_lockWatcher`, and add a `_repositionTimer` (`DispatcherTimer`, ~600 ms) plus a `_repositionPassesLeft` counter. On `Changed`: `passesLeft = 2`, `Stop()` then `Start()` (trailing-edge debounce). On `Tick`: `Stop()`, `PositionPanel()`, then `Start()` again while `--passesLeft > 0` (a settle pass + one confirmation pass for late-settling work areas).
- [x] 2.2 Dispose `_displayWatcher` and stop `_repositionTimer` in the same teardown that disposes `_lockWatcher`.
- [x] 2.3 In `TryGetPrimaryWorkArea`, resolve the primary monitor via `MonitorFromPoint((0,0), MONITOR_DEFAULTTOPRIMARY)` instead of `MonitorFromWindow(_hwnd)`, so the work area (and thus panel height) is the primary's regardless of where the panel currently sits. Remove the now-unused `MonitorFromWindow` import.

## 3. Verify

- [x] 3.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 3.2 Wiring sanity: with the app running, post `WM_DISPLAYCHANGE` to the panel hwnd (or read the rect before/after) to confirm `PositionPanel()` runs and the panel lands on the primary work-area edge (`GetWindowRect` via PowerShell).
- [ ] 3.3 Manual (user): plug in / unplug an external monitor while the app runs; the panel re-docks to the primary display's edge with no restart.
- [x] 3.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-24.

**3.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**3.2 — Wiring sanity (automated).** Launched the app, found the panel hwnd, and drove the message path via user32 P/Invoke from PowerShell:

```
docked rect:   X=1805 Y=12 W=384 H=1131
after shove:   X=100  Y=100          (SetWindowPos to a wrong spot)
after msg:     X=1805 Y=12           (PostMessage WM_DISPLAYCHANGE, waited 800 ms > 400 ms debounce)
RESULT: PASS — panel snapped back to the docked edge
```

This exercises the full path end-to-end: `WM_DISPLAYCHANGE` → `DisplayChangeWatcher` subclass → `Changed` → trailing-edge debounce → `PositionPanel()` → re-dock. Re-run after the primary-resolution + two-pass-debounce fixes (waiting 1700 ms > 2×600 ms): panel still snapped from `X=100` back to the full-height docked rect `X=1805 H=1131` — no regression on a single monitor (`MonitorFromPoint((0,0))` resolves the only display).

**Primary-monitor fix (2.3).** During user testing on a real dock, the panel came in short and its height differed between the parked state (full) and the revealed state (short). Root cause: `TryGetPrimaryWorkArea` resolved the monitor with `MonitorFromWindow(panel)`, which returns the monitor the panel currently overlaps — so the computed height depended on the panel's position, and `ShowPanel`→`PositionPanel` (run while parked off the edge) read the wrong, shorter work area. Switched to `MonitorFromPoint((0,0))`, which always resolves the primary, making the height consistent across parked / sliding / revealed states. The multi-monitor confirmation is the user's manual check (3.3).

**3.3 — Real display change (manual, user).** Plug in / unplug an external monitor (or change resolution / primary display) while the app runs; the panel should re-dock to the primary display's right edge on its own, no restart. Left to the user — a physical monitor change can't be driven from here.

