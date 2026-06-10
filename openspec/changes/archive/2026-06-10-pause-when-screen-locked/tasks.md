## 1. Package

- [x] 1.1 Add `<PackageReference Include="Microsoft.Win32.SystemEvents" Version="*" />` to `src/Huddle.App/Huddle.App.csproj` and confirm restore

## 2. Panel wiring

- [x] 2.1 In `PeekPanelWindow.xaml.cs`, add a `private bool _pausedByLock;` field and a `private SessionSwitchEventHandler? _sessionSwitchHandler;` field
- [x] 2.2 In `OnContentLoaded`, after the scheduler starts, create a `SessionSwitchEventHandler` that dispatches to `this.DispatcherQueue.TryEnqueue(() => HandleSessionSwitch(e))`, store it in `_sessionSwitchHandler`, and subscribe to `Microsoft.Win32.SystemEvents.SessionSwitch`
- [x] 2.3 Add `private void HandleSessionSwitch(SessionSwitchEventArgs e)` implementing the matrix in `design.md` D2 — call `_scheduler.Pause()` / `_scheduler.Resume()` as appropriate, flip `_pausedByLock`, then call `UpdateStatus()` and `UpdateLookBar()`
- [x] 2.4 In `OnPauseClick`, clear `_pausedByLock = false` at the start (manual toggle always overrides)
- [x] 2.5 In `UpdateStatus`, when `_scheduler.IsPaused == true`: render "Paused · screen locked" if `_pausedByLock`, else the existing "Paused · not watching"

## 3. Cleanup

- [x] 3.1 Subscribe to `Window.Closed` in the constructor; in the handler, if `_sessionSwitchHandler` is non-null, `SystemEvents.SessionSwitch -= _sessionSwitchHandler` and null it out
- [x] 3.2 Confirm there are no other lingering `SystemEvents` subscriptions

## 4. Verification

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 4.2 Launch — status reads "Watching · next look in M:SS", look-bar counts down
- [x] 4.3 Press Win+L (lock workstation), wait > 1 second, unlock — between lock and unlock no new moments land in the DB
- [x] 4.4 After unlock, the next tick fires ~3 minutes later (status reads "Watching · next look in M:SS")
- [x] 4.5 Manual pause click while watching: status reads "Paused · not watching"; Win+L while paused does not change anything; unlock while paused does not auto-resume
- [x] 4.6 Lock the workstation first (so the auto-pause kicks in); status updates to "Paused · screen locked" on next status redraw (or on unlock — UI is hidden while locked). Click resume after unlock manually if desired
- [x] 4.7 No exceptions in `Debug` output; no SystemEvents event-handler leaks (verified by closing the window and confirming subscription is gone)
