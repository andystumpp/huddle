## ADDED Requirements

### Requirement: Auto-pause when the workstation locks

The panel SHALL listen for OS session-switch events. When the workstation locks and the scheduler is currently watching, the panel SHALL pause the scheduler and mark the pause as caused by the lock event. When the workstation unlocks, the panel SHALL resume the scheduler only if the most recent pause was caused by a lock event. The user's manual pause/resume actions SHALL always take precedence — clicking pause or play clears the "paused by lock" mark.

#### Scenario: Lock pauses the tick

- **WHEN** the workstation is locked (`SessionSwitch` reason `SessionLock`) and the scheduler is currently watching
- **THEN** the scheduler pauses, the status line reads "Paused · screen locked", and the look-bar drops to 0

#### Scenario: Unlock resumes the auto-paused tick

- **WHEN** the workstation is unlocked (`SessionSwitch` reason `SessionUnlock`) and the most recent pause was caused by a lock event
- **THEN** the scheduler resumes, the look-bar restarts from 0, and the status line returns to "Watching · next look in M:SS"

#### Scenario: User pause survives a subsequent lock+unlock

- **WHEN** the user has manually paused, then the workstation locks and later unlocks
- **THEN** the scheduler stays paused (the unlock does not auto-resume because the most recent pause was the user's, not the lock's)

#### Scenario: Lock does not double-pause an already-paused scheduler

- **WHEN** the user has manually paused and the workstation then locks
- **THEN** the scheduler remains paused; the lock event is recorded but does not change the pause source

#### Scenario: Status line distinguishes auto-pause from manual pause

- **WHEN** the scheduler is paused and the pause was caused by a lock event
- **THEN** the status line reads **"Paused · screen locked"** (not "Paused · not watching")

#### Scenario: First tick after launch while locked

- **WHEN** the app launches with the workstation already locked
- **THEN** the existing immediate-on-start tick still fires once (the OS does not emit a `SessionSwitch` event on its own); the next lock-state transition (unlock) realigns the pause/resume state correctly
