## MODIFIED Requirements

### Requirement: Auto-pause when the workstation locks

The panel SHALL register its own window for WTS session notifications (`WTSRegisterSessionNotification` with `NOTIFY_FOR_THIS_SESSION`) and handle `WM_WTSSESSION_CHANGE` lock and unlock messages. When the workstation locks and the scheduler is currently watching, the panel SHALL pause the scheduler and mark the pause as caused by the lock. When the workstation unlocks, the panel SHALL resume the scheduler only if the most recent pause was caused by the lock. The user's manual pause/resume actions SHALL always take precedence — clicking pause or play clears the "paused by lock" mark.

In addition to the messages, the panel SHALL verify the session's actual lock state (`WTSQuerySessionInformation` session flags) at two points, so that a missed message costs at most one tick interval in either direction: at the top of each capture tick (a locked session skips the capture and engages the lock auto-pause), and once per second while lock-paused (an unlocked session triggers the auto-resume). If the lock state cannot be determined, the session SHALL be treated as unlocked.

#### Scenario: Lock pauses the tick

- **WHEN** a `WM_WTSSESSION_CHANGE` lock message arrives and the scheduler is currently watching
- **THEN** the scheduler pauses, the status line reads "Paused · screen locked", and the look-bar drops to 0

#### Scenario: Unlock resumes the auto-paused tick

- **WHEN** a `WM_WTSSESSION_CHANGE` unlock message arrives and the most recent pause was caused by the lock
- **THEN** the scheduler resumes, the look-bar restarts from 0, and the status line returns to "Watching · next look in M:SS"

#### Scenario: User pause survives a subsequent lock+unlock

- **WHEN** the user has manually paused, then the workstation locks and later unlocks
- **THEN** the scheduler stays paused (the unlock does not auto-resume because the most recent pause was the user's, not the lock's)

#### Scenario: Lock does not double-pause an already-paused scheduler

- **WHEN** the user has manually paused and the workstation then locks
- **THEN** the scheduler remains paused; the lock does not change the pause source

#### Scenario: Status line distinguishes auto-pause from manual pause

- **WHEN** the scheduler is paused and the pause was caused by the lock
- **THEN** the status line reads **"Paused · screen locked"** (not "Paused · not watching")

#### Scenario: Tick while locked skips the capture even if no lock message arrived

- **WHEN** a capture tick fires while the session is locked (e.g., the lock message was never delivered)
- **THEN** no frame is captured, no API call is made, and the lock auto-pause engages exactly as if the lock message had arrived

#### Scenario: Missed unlock self-heals

- **WHEN** the scheduler is lock-paused and the session is observed unlocked by the per-second check (e.g., the unlock message was never delivered)
- **THEN** the auto-resume engages exactly as if the unlock message had arrived

#### Scenario: First tick after launch while locked

- **WHEN** the app launches with the workstation already locked
- **THEN** the immediate-on-start tick skips the capture and engages the lock auto-pause; the panel resumes on unlock as usual

#### Scenario: Lock state query fails

- **WHEN** the session lock-state query fails or reports an unknown state
- **THEN** the session is treated as unlocked: ticks capture normally and the message-based lock handling remains the sole pause source
