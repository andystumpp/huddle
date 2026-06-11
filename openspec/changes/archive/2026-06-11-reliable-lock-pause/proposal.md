## Why

The lock auto-pause shipped in `pause-when-screen-locked` (PR #12) relies on `SystemEvents.SessionSwitch`, which is known to silently never fire on some machines/sessions — and did exactly that overnight on 2026-06-10: the app captured the lock screen every 3 minutes all night, burning Opus calls on "machine is still locked" moments and filling the trail with junk. The design is also edge-triggered only: one missed lock event means capturing forever, with nothing ever re-checking reality.

## What Changes

- Replace `SystemEvents.SessionSwitch` with the direct Win32 mechanism: `WTSRegisterSessionNotification` on the panel's own HWND plus `WM_WTSSESSION_CHANGE` handling (`WTS_SESSION_LOCK` / `WTS_SESSION_UNLOCK`) via window subclassing. Same pause/resume semantics, but registered against a window whose message pump we own.
- Add a level-triggered safety net: at each capture tick, query the session's lock state directly (`WTSQuerySessionInformation` → `WTSINFOEX.SessionFlags`). If the session is locked, skip the capture and engage the lock auto-pause as if the lock event had been received. Worst case after a missed event is one skipped tick, not a night of captures.
- Launching while locked now pauses immediately at the first tick instead of capturing once (the old "first tick fires anyway" scenario is superseded by the tick guard).
- Drop the `Microsoft.Win32.SystemEvents` package reference — nothing else uses it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `app-shell`: The "Auto-pause when the workstation locks" requirement changes its event source from `SystemEvents.SessionSwitch` to WTS session notifications on the panel window, and gains a level-triggered lock check at each capture tick that skips capture and engages the auto-pause when the session is locked.

## Impact

- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — swap event registration, add WndProc subclass, add tick-time lock guard in `OnSchedulerTick`.
- New small helper for the WTS interop (registration, message constants, lock-state query).
- `src/Huddle.App/Huddle.App.csproj` — remove `Microsoft.Win32.SystemEvents` package.
- One-time cleanup (manual, not code): delete the overnight lock-screen junk rows from the `moments` table so they stop polluting the extractor trail.
