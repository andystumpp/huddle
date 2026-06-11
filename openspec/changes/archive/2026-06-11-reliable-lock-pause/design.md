## Context

`pause-when-screen-locked` wired auto-pause to `SystemEvents.SessionSwitch`. On the night of 2026-06-10 the `SessionLock` event was never delivered (a known, silent failure mode of `SystemEvents` — it builds a hidden broker window and registers it for WTS notifications internally; if any step fails there is no error surface), so the tick captured the lock screen every 3 minutes until morning. Two distinct weaknesses:

1. **Unowned plumbing** — we depend on a WinForms-era component's hidden window instead of our own HWND, which we already have (`_hwnd` in `PeekPanelWindow`).
2. **Edge-triggered only** — pause state changes solely on transition events. One missed event and nothing ever re-checks the actual session state.

## Goals / Non-Goals

**Goals:**
- Lock/unlock transitions delivered through plumbing we own end-to-end (our HWND, our message pump).
- A missed transition event can cost at most one tick interval of wrong behavior, in either direction (missed lock → at most one skipped capture; missed unlock → resumes without user action).
- No lock-screen frames are captured or sent to Claude.

**Non-Goals:**
- Idle/away detection beyond the lock screen (sleeping displays, user walked off unlocked — separate concern, not regressed and not added).
- Multi-session / RDP-specific behavior beyond what `NOTIFY_FOR_THIS_SESSION` gives us.
- Automated cleanup of the junk rows already in `moments` (one-time manual step, noted in tasks).

## Decisions

### D1: WTS session notifications on our own HWND, not SystemEvents

`WTSRegisterSessionNotification(_hwnd, NOTIFY_FOR_THIS_SESSION)` + handle `WM_WTSSESSION_CHANGE` (`wParam` = `WTS_SESSION_LOCK` / `WTS_SESSION_UNLOCK`). This is the documented Win32 mechanism, registered against the panel window whose message pump WinUI is already running.

Alternatives considered:
- *Keep SystemEvents, add diagnostics* — still a black box; we'd learn it failed without being able to fix it.
- *ISensLogon (COM)* — works but is heavier (COM event subscription) and adds a dependency where a two-function P/Invoke does the job.
- *WMI watcher on event log 4800/4801* — requires logon auditing enabled; fragile across machines.

### D2: Subclass via `SetWindowSubclass`, not `GWLP_WNDPROC` swap

`SetWindowSubclass`/`RemoveWindowSubclass`/`DefSubclassProc` (comctl32) compose safely if anything else ever subclasses the window, and removal is symmetric. The subclass proc delegate is held in a field so the GC can't collect it. Unregister both the subclass and the WTS notification in `Closed`.

### D3: Level-triggered state check makes the events advisory

`WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSSessionInfoEx)` → `WTSINFOEX.Data.Level1.SessionFlags == WTS_SESSIONSTATE_LOCK`. Checked at two points, both on existing timers (no new timers):

- **Top of `OnSchedulerTick`** (every 180 s while watching): if locked, skip the capture and engage the lock auto-pause exactly as if `WTS_SESSION_LOCK` had arrived. Covers the missed-lock case and the launched-while-locked case.
- **In the existing 1 s status timer, only while `_pausedByLock`**: if the session is no longer locked, auto-resume exactly as if `WTS_SESSION_UNLOCK` had arrived. Covers the missed-unlock case (without this, a missed unlock leaves the app paused forever with a lying status line). One cheap local API call per second, only while lock-paused.

The WM_WTSSESSION_CHANGE messages remain the fast path (instant UI state flip); the polled checks are the source of truth. Note the constant quirk: `WTS_SESSIONSTATE_LOCK` is `0`, `WTS_SESSIONSTATE_UNLOCK` is `1`, unknown is `0xFFFFFFFF`. (Windows 7 inverted these; our floor is 17763, so irrelevant.)

### D4: Fail open on UNKNOWN

If the query fails or returns `WTS_SESSIONSTATE_UNKNOWN`, treat the session as unlocked: capture proceeds, no auto-resume is suppressed. A broken query must not brick the product's core loop; the event path still provides lock coverage in that case.

### D5: One new file, `SessionLockWatcher`, owning all of it

A single class in `src/Huddle.App/Capture/SessionLockWatcher.cs`: takes the HWND, performs registration + subclassing, exposes `Locked`/`Unlocked` events (raised on the window's thread — WM messages arrive there, so no marshalling needed, simpler than the old `DispatcherQueue.TryEnqueue` dance) and a static `IsSessionLocked()` for the polled checks. `PeekPanelWindow` keeps only the pause/resume policy (`_pausedByLock`, user-pause precedence), which is unchanged. `Microsoft.Win32.SystemEvents` package reference is removed.

## Risks / Trade-offs

- [`WTSRegisterSessionNotification` can return false (e.g., Terminal Services service hiccup at startup)] → log via `Debug.WriteLine`; the polled checks still bound the damage to one tick.
- [Subclass proc leaks or fires after window destruction] → store the delegate in a field, `RemoveWindowSubclass` + `WTSUnRegisterSessionNotification` in `Closed`, before the timers are stopped.
- [Polling adds API calls] → one call per 180 s while watching, one per second only while lock-paused. Negligible.
- [Lock/unlock verification needs a real lock cycle] → `rundll32 user32.dll,LockWorkStation` triggers a real lock; manual verification steps go in tasks.md §Verification per project convention.

## Open Questions

None — the pause/resume policy and all UI behavior are unchanged from the current spec; only the detection mechanism and the self-healing checks change.
