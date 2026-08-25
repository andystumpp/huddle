# Design — reposition on display change

The dock math already exists and is correct; what's missing is a trigger. This change adds one: a window-message watcher that, after a display change settles, re-invokes `PositionPanel()`.

## Flow

```mermaid
sequenceDiagram
    participant OS as Windows
    participant W as DisplayChangeWatcher<br/>(window subclass)
    participant P as PeekPanelWindow
    participant T as Debounce timer<br/>(DispatcherTimer ~600ms, 2 passes)
    participant PP as PositionPanel()

    Note over OS: monitor plugged/unplugged,<br/>resolution / DPI / work area changes
    OS->>W: WM_DISPLAYCHANGE / WM_SETTINGCHANGE(SPI_SETWORKAREA)
    W->>W: DefSubclassProc(...) (pass through)
    W-->>P: Changed (on the UI thread)
    P->>T: passesLeft=2; Stop() then Start()  (reset — trailing edge)
    Note over OS,W: further burst messages each reset the timer
    T-->>P: Tick (quiet for ~600ms)
    P->>PP: PositionPanel()  (settle pass)
    P->>T: if --passesLeft > 0: Start() again
    T-->>P: Tick (~600ms later)
    P->>PP: PositionPanel()  (confirmation pass)
    PP->>OS: MoveAndResize panel + tab to primary work area
```

**Contract crossing each boundary**

- `DisplayChangeWatcher(IntPtr hwnd)` — subclasses `hwnd` via `SetWindowSubclass`, holds the `SUBCLASSPROC` delegate in a field (GC-safety), and raises `event EventHandler Changed`. It fires `Changed` on `WM_DISPLAYCHANGE` and on `WM_SETTINGCHANGE` when `wParam == SPI_SETWORKAREA`; every message is still forwarded to `DefSubclassProc`. `Dispose()` removes the subclass. Messages arrive on the window's own thread, so `Changed` handlers may touch UI state directly (same contract as `SessionLockWatcher`).
- `PeekPanelWindow` — owns a `_displayWatcher`, a `_repositionTimer` (`DispatcherTimer`, ~600 ms), and a `_repositionPassesLeft` counter. `Changed` → set `passesLeft = 2`, `Stop()`+`Start()` (trailing-edge debounce). `Tick` → `Stop()`, `PositionPanel()`, and `Start()` again while `--passesLeft > 0`. Created next to `_lockWatcher` and disposed/stopped in the same teardown.
- `PositionPanel()` — layout math unchanged, but `TryGetPrimaryWorkArea` now resolves the **primary** monitor via `MonitorFromPoint((0,0), MONITOR_DEFAULTTOPRIMARY)` rather than `MonitorFromWindow(panel)`. Idempotent: calling it when nothing moved is a no-op reposition to the same rect.

## Resolving the primary monitor (not the panel's monitor)

`TryGetPrimaryWorkArea` was named for the primary but resolved the monitor with `MonitorFromWindow(_hwnd, MONITOR_DEFAULTTOPRIMARY)`. `MonitorFromWindow` returns the monitor the window's rectangle **overlaps** — the flag is only a fallback for a fully off-screen window. So the work area (hence the panel's height and dock edge) depended on where the panel currently sat. After a display switch it read whatever monitor the stale rect touched — often the shorter one — which surfaced as a panel that reveals short, and as a height that differs between the parked state (last full-height dock) and the revealed state (`ShowPanel` re-runs `PositionPanel` while the panel sits parked off the edge).

The primary monitor's top-left is `(0,0)` in virtual-screen coordinates by definition, so `MonitorFromPoint((0,0), …)` resolves the primary independently of the panel's position. Every `PositionPanel` call now computes the same primary work area, so the height is consistent across parked / sliding / revealed states.

## Why trailing-edge debounce, plus a confirmation pass

`WM_DISPLAYCHANGE` is delivered *after* Windows applies a change, but a full dock is multi-stage: the external attaches, the mode is set, the primary is reassigned, and work areas recompute — spread across a burst of messages, with the new primary's work area sometimes settling a beat after the last one. Trailing-edge debounce (reset on each message) makes the *last* message trigger the reposition; the second **confirmation pass** ~600 ms later catches a work area that finalized just after the first pass. Repositioning is idempotent, so the confirmation is a no-op whenever the settle pass already landed right. The exact interval is not load-bearing — it only needs to out-wait the burst.

## Why a separate watcher (not the existing one)

`SessionLockWatcher` owns lock/unlock semantics; display changes are an unrelated concern. Per the codebase's single-responsibility norm, `DisplayChangeWatcher` is its own file mirroring the same subclass mechanics. Two subclasses on one hwnd coexist fine — `SetWindowSubclass` keys on a distinct `uIdSubclass`.

## Deliberately omitted

- **Per-monitor tracking / "stay on the current screen".** The panel targets the primary display; following a specific non-primary monitor is a separate feature, not this bug fix.
- **`WM_DPICHANGED` handling.** A topology change already arrives as `WM_DISPLAYCHANGE`, and `PositionPanel` re-reads `GetDpiForMonitor` each run, so DPI is covered without a second handler that could contend with the framework.
