## Why

The tick is firing while the screen is locked. Recent moments include "Screen has been black for 15+ minutes — you're idle or the machine is sleeping" and "Screen is still black — you're idle or the display has been sleeping for the past several minutes" — both correct observations, and both ~$0.006 API calls capturing literally nothing. ADR 0001 D1 always intended *"paused when system idle"*; we deferred it when shipping the tick loop, and the lock-screen case is now visibly the right time to add it back.

Lock-screen detection is the smaller, more obvious win and the one with a clean Windows-level signal. System-idle-while-unlocked (mouse / keyboard inactive for N min) is a related but separate signal — useful, but layered on later if needed.

## What Changes

- Auto-pause the tick whenever the workstation locks; auto-resume when it unlocks.
- Status line distinguishes auto-pause from manual pause: when auto-paused it reads **"Paused · screen locked"** (instead of the existing **"Paused · not watching"**).
- The user's manual pause/play actions take precedence. If the user clicks pause while unlocked, then the screen locks, the lock event does nothing extra (already paused). If the screen locks (auto-pause), the user then clicks resume, the user's choice wins and the next unlock event does *nothing* (no double resume).
- Detection via `Microsoft.Win32.SystemEvents.SessionSwitch` — small official NuGet package, marshals `WM_WTSSESSION_CHANGE` into a clean managed event. No P/Invoke required.
- Subscribe on panel load, unsubscribe on close (so event handlers don't leak across hot-reload).

## Capabilities

### Modified Capabilities
- `app-shell`: pause-state semantics now include an auto-paused variant driven by OS session-lock events, with a distinct status line. The existing user-driven pause / resume requirements stay in force; auto behavior layers on top.

## Impact

- `src/Huddle.App/Huddle.App.csproj` — add `<PackageReference Include="Microsoft.Win32.SystemEvents" Version="*" />`.
- `src/Huddle.App/Vision/TickScheduler.cs` — already exposes `IsPaused`, `Pause()`, `Resume()`. No surface change needed; we drive it from the panel.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — subscribe to `SystemEvents.SessionSwitch` in `OnContentLoaded`; add a `_pausedByLock` field; handle `SessionLock` (auto-pause if not already paused) and `SessionUnlock` (auto-resume only if we were the ones who paused); update the existing `OnPauseClick` to clear `_pausedByLock` when the user toggles; tweak `UpdateStatus` to render "Paused · screen locked" when `_pausedByLock` is true. Unsubscribe the event handler when the window closes.
- No DB schema change. No UI structural change beyond the status-line wording.

## Cost note

Every avoided lock-screen tick is ~$0.006 saved. If you usually lock for 30 minutes at lunch and a 90-minute meeting, that's ~40 ticks/day that won't fire, ~$0.25/day, ~$5/month. Small but real, and the moments produced from locked screens are noise anyway.
