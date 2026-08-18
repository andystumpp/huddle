# app-shell Specification

## Purpose

The visible surface of Huddle: a single docked peek-panel window that runs as a standard Windows desktop app and renders the panel's chrome, header, look-bar countdown, and scenario filters. Pipeline-fed nudge content (cards, stream) lands in a later capability; this one defines the surface those nudges will render into.
## Requirements
### Requirement: Window-based app lifecycle

The Huddle app SHALL run as a standard Windows desktop application with a single top-level window. Launching the executable SHALL show the peek panel window immediately and add a Huddle entry to the Windows taskbar. The app SHALL NOT register a system tray icon or tray context menu. Closing the panel window SHALL terminate the process.

#### Scenario: Launch shows the window

- **WHEN** the user launches `Huddle.exe`
- **THEN** the peek panel window is visible on the desktop, docked to the bottom-right of the primary display's work area, and a Huddle entry appears in the taskbar

#### Scenario: No tray icon

- **WHEN** the app is running
- **THEN** no Huddle icon appears in the Windows system tray

#### Scenario: Close exits the app

- **WHEN** the user closes the panel window (Alt+F4, taskbar context menu)
- **THEN** the Huddle process terminates and the taskbar entry disappears

### Requirement: Peek panel placement and chrome

The peek panel SHALL be a 384 px wide, borderless top-level window with no native title bar or caption buttons. It SHALL dock 12 px from the right edge of the primary display's work area and SHALL stretch vertically to the full work-area height minus a 12 px gap at the top and a 12 px gap at the bottom, with a 320 px minimum height. The panel window SHALL request DWM round corners (`DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`) and its content SHALL be clipped at an 8 px corner radius to match. Resize, minimize, and maximize SHALL be disabled. The panel SHALL recompute its dock position and slide geometry whenever it is shown. The panel SHALL remain always-on-top while open.

#### Scenario: Panel stretches the work-area height

- **WHEN** the app launches on a standard single-monitor setup
- **THEN** the panel's top edge sits 12 px below the work area's top edge, its bottom edge sits 12 px above the work area's bottom edge, and its right edge sits 12 px from the work area's right edge

#### Scenario: Panel has no native title bar

- **WHEN** the panel is visible
- **THEN** no native title bar, caption text, or caption buttons (minimize / maximize / close) are drawn at the top of the window

#### Scenario: Window corners are rounded

- **WHEN** the panel is visible
- **THEN** all four corners of the window — acrylic backdrop included — render rounded, with the content's 8 px clip tracing the window's corner curve

#### Scenario: Panel cannot be resized

- **WHEN** the user attempts to drag a window edge or click maximize / minimize
- **THEN** the window size does not change

### Requirement: Panel slide-out and slide-in

The panel SHALL hide itself by sliding fully off the right edge of the work area, and reveal itself by sliding back to its docked position. The panel SHALL slide out after the cursor has been outside the panel's visible bounds for 700 ms, and SHALL slide in when the cursor enters the peek chip's hover zone at the right screen edge. Each slide SHALL animate horizontally over 220 ms with an ease-out cubic curve, keeping the panel's vertical position fixed.

#### Scenario: Panel slides out when the cursor leaves

- **WHEN** the panel is visible and the cursor stays outside the panel's bounds for 700 ms
- **THEN** the panel animates off-screen to the right over 220 ms and is no longer visible in the work area

#### Scenario: Cursor returning within the grace period cancels the hide

- **WHEN** the panel is visible, the cursor leaves the panel, and returns within 700 ms
- **THEN** the panel stays visible and the grace timer restarts on the next leave

#### Scenario: Hovering the chip slides the panel in

- **WHEN** the panel is hidden and the cursor enters the peek chip's hover zone
- **THEN** the panel animates from off-screen to its docked position over 220 ms

### Requirement: Peek chip window

While the panel is hidden, the app SHALL show a separate always-on-top chip window at the right edge of the work area: 28 px of visible width, 168 px tall, vertically centered. The chip SHALL be shaped as a left-rounded tab via a window region whose right corners fall past the screen edge. Because the OS clamps top-level windows to a minimum track width (~133 px), the chip window SHALL anchor its content to the left 28 px — the on-screen portion — and let the clamped excess hang past the screen edge. The chip SHALL be hidden whenever the panel is visible: it disappears when a slide-in starts and appears when a slide-out completes.

#### Scenario: Chip appears when the panel finishes hiding

- **WHEN** the panel's slide-out animation completes
- **THEN** the chip is visible at the right edge of the work area, vertically centered, showing the unread count

#### Scenario: Chip disappears when the panel returns

- **WHEN** a slide-in begins
- **THEN** the chip window is hidden before the panel reaches its docked position

#### Scenario: Chip content sits in the on-screen sliver

- **WHEN** the chip is visible
- **THEN** the count renders within the leftmost 28 px of the chip window — the portion on screen — regardless of the actual clamped window width

### Requirement: Unread nudge count on the chip

The chip SHALL display the number of nudges that arrived while the panel was not "seen". A nudge increments the unread count when it is inserted and the panel has not been open for the read-grace period. The unread count SHALL reset to 0 — and the panel SHALL be marked seen — once the panel has stayed open for 3 seconds; sliding out before that preserves the count. While the unread count is greater than zero, the chip SHALL render a pulsing halo behind the number; at zero the halo is still.

#### Scenario: Nudges arriving while hidden increment the count

- **WHEN** the panel is hidden and a scenario emits two nudges
- **THEN** the chip shows "2" with a pulsing halo

#### Scenario: Count resets after the panel is open 3 seconds

- **WHEN** the panel slides in and stays open for 3 seconds
- **THEN** the unread count resets to 0 and the chip shows "0" with no pulse on the next slide-out

#### Scenario: A quick peek preserves the count

- **WHEN** the panel slides in and slides back out in under 3 seconds
- **THEN** the chip still shows the previous unread count

### Requirement: Panel background and aurora sheen

The peek panel SHALL use a DesktopAcrylic system backdrop (or, on systems where acrylic is unavailable, a translucent dark fallback tint). The panel SHALL render two soft radial gradient overlays above the backdrop — a warm coral / pink wash anchored to the top-right corner, and a cool violet wash anchored to the bottom-left — matching the prototype's `data-style="aurora"` `::before` sheen. The panel SHALL use the Dark element theme regardless of the system's theme setting.

#### Scenario: Acrylic backdrop

- **WHEN** the panel is visible on a system with transparency effects enabled
- **THEN** the backdrop renders with DesktopAcrylic material

#### Scenario: Aurora sheen

- **WHEN** the panel is visible
- **THEN** a soft coral/pink radial wash is visible near the top-right corner, and a soft violet radial wash is visible near the bottom-left corner, both layered above the acrylic

#### Scenario: Dark theme is forced

- **WHEN** the user's Windows theme is set to Light
- **THEN** the panel still renders with dark-theme colors (light text on dark backdrop)

### Requirement: Look-bar progress hairline

The peek panel SHALL display a 2 px progress hairline at its top edge that fills horizontally as the next-look countdown advances. The countdown period SHALL be 180 seconds (3 minutes), matching the capture tick. When the countdown reaches zero, the hairline SHALL reset to empty and begin filling again. When the app is paused, the hairline SHALL be empty and stationary.

#### Scenario: Hairline fills over the 3-minute tick

- **WHEN** the watching countdown advances from 180 s to 0
- **THEN** the look-bar's filled width goes from 0% to 100% over that span

#### Scenario: Pause clears the hairline

- **WHEN** the app is paused
- **THEN** the look-bar is empty (0% width) and not animating

### Requirement: Panel header — brand, status, and controls

The peek panel SHALL display a header containing the Huddle mark, the brand name "Huddle", a status line, a pause/resume button, and a settings button. When the app is watching, the status line SHALL show a green pulsing dot, the text **"Watching · next look in M:SS"**, and the countdown SHALL match the look-bar. When the app is paused, the status line SHALL show **"Paused · not watching"** without the pulsing dot, and the look-bar SHALL stop advancing. The pause button SHALL toggle between watching and paused states, swapping its icon between pause and play glyphs. The settings button SHALL be present but MAY be a no-op stub.

#### Scenario: Watching shows a live countdown

- **WHEN** the app is in the watching state
- **THEN** the status line reads "Watching · next look in M:SS" and the seconds value decrements once per second

#### Scenario: Watch-dot pulses

- **WHEN** the app is in the watching state
- **THEN** a green dot is shown next to the status text with a slow pulsing halo around it

#### Scenario: Pause stops the countdown

- **WHEN** the user clicks the pause button while watching
- **THEN** the status line changes to "Paused · not watching", the pulsing dot disappears, the look-bar drops to 0%, and the pause icon swaps to a play icon

#### Scenario: Resume restarts the countdown

- **WHEN** the user clicks the play button while paused
- **THEN** the status line returns to "Watching · next look in M:SS", the look-bar resumes from zero, and the icon swaps back to pause

### Requirement: Empty state

When there are no nudges to show (either none exist, or none match the current filter), the panel SHALL display a centered empty state below the filter chips, containing a small spark glyph, the message **"No {scenario} nudges right now."** (or **"No nudges right now."** when the All filter is active), and the subtitle **"Huddle is watching — something useful will surface soon."**

#### Scenario: Empty state on first launch

- **WHEN** the panel is launched and the nudge store is empty
- **THEN** the empty state spark glyph, the "No nudges right now." line, and the watching subtitle are visible

#### Scenario: Empty state subtitle wording

- **WHEN** the empty state is visible
- **THEN** the subtitle reads "Huddle is watching — something useful will surface soon."

### Requirement: Nudges / Activity tab strip

The peek panel SHALL display a two-tab strip directly below the panel header, replacing the previous filter chips. The tabs SHALL be **Nudges** (with a lightbulb glyph) and **Activity** (with a pulse / activity glyph). Each tab SHALL display its label and a numeric count badge — Nudges count = the number of visible nudges; Activity count = the number of patterns detected. Exactly one tab SHALL be selected at a time; the selected tab SHALL be rendered with a brighter background, a colored accent border on its left edge, and full-opacity label text, while the unselected tab uses the muted chip surface. On first launch, the **Activity** tab SHALL be selected by default.

#### Scenario: Both tabs are visible

- **WHEN** the panel is visible
- **THEN** the Nudges tab and the Activity tab are both visible below the header, side by side, each showing a glyph, a label, and a count

#### Scenario: Activity is the default

- **WHEN** the panel is launched
- **THEN** the Activity tab is selected and its content (the patterns-detected section) is shown below

#### Scenario: Selecting Nudges switches the surface

- **WHEN** the user clicks the Nudges tab
- **THEN** the Nudges tab becomes selected, the Activity tab is deselected, and the content area swaps to the nudges empty state

#### Scenario: Selecting an already-selected tab keeps it selected

- **WHEN** the user clicks the currently-selected tab
- **THEN** that tab stays selected (it does not toggle off)

#### Scenario: Tab counts reflect the data

- **WHEN** the panel loads with N patterns and 0 nudges
- **THEN** the Activity tab shows the count "N" and the Nudges tab shows "0"

### Requirement: Activity tab content — observations

When the Activity tab is selected, the content area SHALL display a section header reading **"OBSERVATIONS N"** in uppercase with a small circled-plus glyph to its left, where N is the number of moments currently rendered. Below the header, the panel SHALL render a vertically scrollable list of moment cards, ordered newest-first. The rendered list SHALL be capped at the 20 most recent moments; older moments stay in the store but are not displayed in this change.

#### Scenario: Section header shows the count

- **WHEN** the Activity tab is selected and N moments are loaded
- **THEN** the section header reads "OBSERVATIONS N" (uppercase) with a circled-plus glyph to its left

#### Scenario: Moments listed newest first

- **WHEN** the panel has loaded multiple moments
- **THEN** they render top-to-bottom from largest `ts` to smallest

#### Scenario: New moments appear at the top in real time

- **WHEN** the tick completes a successful capture while the panel is open
- **THEN** the new moment is inserted at position 0 of the visible list without restarting the app

#### Scenario: Older moments fall off the visible list

- **WHEN** the panel already shows 20 moments and a new one arrives
- **THEN** the new moment is shown at the top and the oldest is removed from the visible list (it remains in the store)

### Requirement: Card relative timestamps

Cards in the peek panel SHALL display a relative timestamp derived from the record's `ts`. The label text SHALL be:

- **"just now"** when the age is under 60 seconds,
- **"Nmin ago"** for a whole-minute age from 1 to 59 minutes (e.g. `3min ago`),
- **"Nh ago"** for a whole-hour age from 1 to 23 hours (e.g. `2h ago`),
- **"Nd ago"** for a whole-day age of 1 day or more (e.g. `5d ago`).

The formatting SHALL be produced by a single shared helper used by every card type. The panel SHALL drive a single shared clock that ticks at least once per minute while the panel is open and refreshes every visible card's relative timestamp in place, without re-rendering or reordering the card lists. Cards SHALL subscribe to the clock when they are realized and unsubscribe when they leave the visual tree (the lists are virtualized).

#### Scenario: Fresh card reads "just now"

- **WHEN** a card's `ts` is less than 60 seconds before the current time
- **THEN** its relative timestamp reads "just now"

#### Scenario: Minute-old card reads "Nmin ago"

- **WHEN** a card's `ts` is 3 minutes before the current time
- **THEN** its relative timestamp reads "3min ago"

#### Scenario: Hour-old card reads "Nh ago"

- **WHEN** a card's `ts` is 2 hours before the current time
- **THEN** its relative timestamp reads "2h ago"

#### Scenario: Day-old card reads "Nd ago"

- **WHEN** a card's `ts` is 5 days before the current time
- **THEN** its relative timestamp reads "5d ago"

#### Scenario: Open panel refreshes labels over time

- **WHEN** the panel stays open and a card that read "just now" crosses the one-minute mark
- **THEN** the shared clock updates that card's label to "1min ago" in place, with no list re-render

### Requirement: Moment card content

Each moment card SHALL show the model's 1–2 sentence summary text as the main content, followed by a single footer row containing the source app's monogram tile (via `AppTile`), the foreground window title, and a relative timestamp derived from `moment.ts` (per the *Card relative timestamps* requirement) aligned to the row's right edge. The window title SHALL trim with character-ellipsis if it doesn't fit between the tile and the timestamp on a single line. Moments are scenario-neutral observations — no scenario tag, no scenario rail, no nudge badge.

#### Scenario: Card shows summary, source tile, title, and timestamp

- **WHEN** a moment card renders
- **THEN** the summary text is visible as the main body, and the footer shows one `AppTile`, the window title, and the relative timestamp

#### Scenario: Window title is trimmed when too long

- **WHEN** a moment's window title exceeds the available footer width on a single line
- **THEN** the title is trimmed with a trailing ellipsis (no wrapping to a second line), while the relative timestamp stays fully visible

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

### Requirement: Nudges tab day-grouped review

The Nudges tab SHALL present nudges from the last 7 days, grouped under day headers with the newest day first. Each group SHALL be introduced by a header labeled `TODAY`, `YESTERDAY`, or the local date (e.g. `MON · AUG 18`) for older days, and the nudges under a header SHALL be those emitted on that local day, newest first. New nudges emitted while the panel is open SHALL appear under the correct day group without a reload.

#### Scenario: Nudges are grouped by day

- **WHEN** the panel opens with nudges emitted across several days within the last week
- **THEN** the Nudges tab shows a `TODAY` header above today's nudges, a `YESTERDAY` header above yesterday's, and a dated header above each older day, newest day first

#### Scenario: A new nudge joins today's group live

- **WHEN** a scenario emits a nudge while the panel is open
- **THEN** it appears at the top of the `TODAY` group without reopening the panel

#### Scenario: Only the last 7 days are loaded

- **WHEN** the Nudges tab loads
- **THEN** it queries nudges emitted at or after 7 days ago and renders those, rather than a flat fixed-count list

### Requirement: Nudges tab scenario filter

The Nudges tab SHALL provide a single-select filter that isolates one scenario's nudges. The filter SHALL offer `All` plus one option per scenario, default to `All`, and re-group the visible nudges by day when the selection changes.

#### Scenario: Filtering to one scenario

- **WHEN** the user selects the Achievements filter
- **THEN** the list shows only Achievements nudges, still grouped under day headers, and empty days disappear

#### Scenario: Returning to all scenarios

- **WHEN** the user selects `All`
- **THEN** the list shows every scenario's nudges again, grouped by day

#### Scenario: The filter is single-select

- **WHEN** the user selects a scenario chip while another is active
- **THEN** the newly selected chip becomes the only active one
