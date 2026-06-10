## Context

The tick fires every 3 minutes and captures the foreground display + windows. When the workstation is locked, the foreground app is `LogonUI.exe`, the desktop is black, and the moments produced are useless ("Screen is still black — you're idle"). ADR 0001's tick spec calls for pausing when the system is idle; we explicitly deferred that detection. The lock-screen case is the lowest-effort, highest-signal piece of that work — Windows emits a clean event when the workstation locks and unlocks, and the right behavior is obvious (stop firing the tick).

System-idle-while-unlocked (e.g. "screen on, but you've been on a phone call for 15 minutes") is a related but distinct signal. We can layer it on later via `GetLastInputInfo` if locked-only doesn't cover the no-useful-work case well enough.

## Goals / Non-Goals

**Goals:**

- Tick stops firing as soon as the workstation locks.
- Tick resumes firing on unlock (next tick fires after a full 180-second cycle, same as a manual resume).
- The user's manual pause/resume actions always take precedence over auto-pause/resume.
- Status line tells the user *why* the panel is paused: "Paused · screen locked" vs "Paused · not watching".
- No event-handler leaks across window lifecycle.

**Non-Goals:**

- **No input-idle detection** (mouse/keyboard inactivity while unlocked). Layered on as a separate change if needed.
- **No screensaver detection.** Modern Windows uses lock instead of screensaver for security; if a user has a screensaver without lock, the tick will fire. Edge case, accept.
- **No remote-desktop / RDP-disconnect handling.** `SystemEvents.SessionSwitch` actually does fire `RemoteConnect`/`RemoteDisconnect` reasons; we ignore them in this change. If RDP usage becomes a real path, we'll handle it then.
- **No suspend / hibernate detection.** Windows pauses our process during sleep anyway; ticks resume on wake and one captured frame from the just-woken state is fine.
- **No persisted auto-paused state across launches.** State is in-memory only — if the app launches while the screen is locked, the tick still fires once at startup. Cheap edge case to accept; a session-switch event on the *next* unlock fixes it.

## Decisions

### D1. `Microsoft.Win32.SystemEvents` over raw P/Invoke

- **Choice:** Add `Microsoft.Win32.SystemEvents` NuGet package, subscribe to `SystemEvents.SessionSwitch`.
- **Rationale:** First-party Microsoft package, ~100 KB, handles all the WTS / message-pump plumbing under the hood. The alternative — `WTSRegisterSessionNotification` + WndProc subclassing — is several dozen lines of P/Invoke + interop for a one-event subscription. Not worth it.
- **Trade-off:** A small new dependency for a feature that *could* be hand-rolled. Accepted; this is the canonical .NET way to get session events and we're not allergic to first-party libraries.

### D2. Two pause "sources"

- **Choice:** Two boolean fields on the panel: the existing `_scheduler.IsPaused` (driven by manual toggle) and a new `_pausedByLock`. The tick scheduler doesn't know anything about lock state; the panel owns the policy.
- **Behavior matrix:**

  | event | `_scheduler.IsPaused` before | `_pausedByLock` before | action |
  |---|---|---|---|
  | Manual pause-click | false | * | `Pause()`; clear `_pausedByLock` |
  | Manual play-click | true | * | `Resume()`; clear `_pausedByLock` |
  | OS `SessionLock` | false | false | `Pause()`; set `_pausedByLock = true` |
  | OS `SessionLock` | true | * | no-op (already paused, whatever the reason) |
  | OS `SessionUnlock` | true | true | `Resume()`; clear `_pausedByLock` |
  | OS `SessionUnlock` | true | false | no-op (user paused; don't override) |
  | OS `SessionUnlock` | false | * | no-op (already running) |

- **Rationale:** The matrix is small and easy to reason about. Users get the final say; the OS gets to nudge the panel into pause when it'd otherwise burn API spend on blank frames.

### D3. Status-line wording

- **Choice:** When `_pausedByLock` is true *and* the scheduler is paused, the status line reads **"Paused · screen locked"**. Otherwise the existing **"Paused · not watching"** stays.
- **Rationale:** Self-explanatory, parallels the existing "Watching · next look in M:SS" wording. The user can see at a glance whether the pause was their choice or the OS's.

### D4. Subscribe on panel load, unsubscribe on close

- **Choice:** Subscribe to `SystemEvents.SessionSwitch` in `OnContentLoaded` (after the scheduler starts). Unsubscribe in `Window.Closed`.
- **Rationale:** `SystemEvents` holds strong references to its subscribers; not unsubscribing causes the panel + window to be rooted forever. Hot-reload during development would compound the leak. The app currently exits on close, so the leak is technically benign — but explicit cleanup is the right discipline.

### D5. Marshal session events onto the UI thread

- **Choice:** `SystemEvents.SessionSwitch` fires on a system-events thread, not the UI thread. The handler dispatches to the panel's `DispatcherQueue` before touching state or UI.
- **Rationale:** `_scheduler.Pause()` / `Resume()`, status-line updates, and the look-bar all need to run on the UI thread. WinUI 3 throws or no-ops on cross-thread access.

### D6. Defer the first tick if the app launches while locked

- **Choice:** The `TickScheduler` already fires once on `Start()`. If the screen is locked at launch, we have no way to know from `SystemEvents` (which only fires on transitions). One ill-spent tick on launch is acceptable; the unlock will catch up.
- **Alternative considered:** Probe lock state at startup via `OpenInputDesktop()` — returns NULL when the lock screen owns input. Workable, but adds P/Invoke we don't otherwise need. Skip until the launch-while-locked case becomes a real complaint.

## Risks / Trade-offs

- **[`SystemEvents.SessionSwitch` is documented but adds a NuGet dep]** → Accepted. Tiny package, first-party.
- **[Unsubscribing on `Window.Closed` requires us to remember to subscribe with the same delegate]** → Standard pattern. We store the handler in a field.
- **[Auto-resume could surprise the user if they expected silence after locking]** → Mitigated by the status-line wording: the user can see the panel auto-pauses; if they want it to stay paused after unlock, they can pause manually before locking.
- **[Race: lock fires while a tick is in-flight]** → The in-flight capture / Claude call completes normally; only future ticks are suppressed. Same semantics as the existing manual pause.
- **[Race: SystemEvents fires before `OnContentLoaded`]** → Can't happen — we subscribe inside `OnContentLoaded`, so the first event we see is after the panel is constructed and the scheduler started.

## Open Questions

- Should auto-pause have a small grace period (e.g. don't pause if the lock is held for < 30 seconds — covers brief security-policy locks)? Defaulting to "pause immediately, no grace" — simpler, and the cost of one ill-saved tick is trivial.
- Should the look-bar render differently when auto-paused vs user-paused? Currently both render as 0% width and no animation. Different visual could communicate the cause without reading the status line. Not in this change; we'll see if the wording alone is enough.
