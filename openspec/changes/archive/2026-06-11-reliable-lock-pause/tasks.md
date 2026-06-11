## 1. SessionLockWatcher

- [x] 1.1 Create `src/Huddle.App/Capture/SessionLockWatcher.cs`: P/Invoke declarations for `WTSRegisterSessionNotification` / `WTSUnRegisterSessionNotification` (wtsapi32), `SetWindowSubclass` / `RemoveWindowSubclass` / `DefSubclassProc` (comctl32), and `WTSQuerySessionInformation` / `WTSFreeMemory`; constants `WM_WTSSESSION_CHANGE` (0x02B1), `WTS_SESSION_LOCK` (0x7), `WTS_SESSION_UNLOCK` (0x8), `NOTIFY_FOR_THIS_SESSION` (0)
- [x] 1.2 Instance API: constructor takes the HWND, registers the WTS notification and installs the subclass (delegate held in a field); `Locked` / `Unlocked` events raised directly from the subclass proc (messages arrive on the window's thread — no dispatcher marshalling); `Dispose` removes the subclass and unregisters the notification; failed registration logs via `Debug.WriteLine` and leaves events silent
- [x] 1.3 Static `IsSessionLocked()`: `WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSSessionInfoEx)` → `WTSINFOEX.Data.Level1.SessionFlags`; return true only for `WTS_SESSIONSTATE_LOCK` (0); query failure or `WTS_SESSIONSTATE_UNKNOWN` returns false (fail open); always `WTSFreeMemory`

## 2. PeekPanelWindow rewiring

- [x] 2.1 Replace the `SystemEvents.SessionSwitch` registration in `OnContentLoaded` with a `SessionLockWatcher` instance; `Locked` → existing lock-pause path, `Unlocked` → existing lock-resume path (extract the two branches of `HandleSessionSwitch` into `PauseForLock()` / `ResumeFromLock()` and delete `HandleSessionSwitch` and the `Microsoft.Win32` using)
- [x] 2.2 Dispose the watcher in `OnWindowClosed` (replacing the `SystemEvents` unsubscribe)
- [x] 2.3 Tick guard: at the top of `OnSchedulerTick`, if `SessionLockWatcher.IsSessionLocked()` then call `PauseForLock()` and return before any capture/API work
- [x] 2.4 Self-heal resume: in the existing 1 s status-timer tick, when `_pausedByLock` and `!IsSessionLocked()`, call `ResumeFromLock()`
- [x] 2.5 Remove the `Microsoft.Win32.SystemEvents` package reference from `Huddle.App.csproj`

## 3. Verification

- [x] 3.1 `dotnet build Huddle.slnx -c Debug` passes
- [x] 3.2 Manual: launch the exe, confirm "Watching" state, run `rundll32 user32.dll,LockWorkStation`, unlock after >10 s — status line showed "Paused · screen locked" during the lock (visible on unlock before resume kicks in within ~1 s) and returns to "Watching · next look in M:SS"; no moment row with a lock-screen summary was added during the lock (check the Activity tab)
- [x] 3.3 Manual: pause manually, lock + unlock — panel stays paused ("Paused · not watching"), play button resumes
- [ ] 3.4 Manual (tick guard, simulates a missed lock event): temporarily comment out the `Locked` event subscription, lock the workstation across a tick boundary, unlock — no lock-screen moment was captured and the panel auto-paused then auto-resumed; restore the subscription
- [x] 3.5 One-time cleanup: delete the overnight junk rows — `DELETE FROM moments WHERE app = 'LockApp.exe'` against `%LOCALAPPDATA%\Huddle\huddle.db` (verify the app filter matches the junk rows first with a SELECT)
