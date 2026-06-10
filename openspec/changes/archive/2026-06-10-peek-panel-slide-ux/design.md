# Design — peek panel slide UX

> Retrofit: decisions below were made during implementation (branch
> `peek-panel-slide-ux`, commit `556c085`); recorded here with the rationale and
> the dead ends, because the dead ends are the useful part.

## Context

The panel was a fixed 460dip always-on-top window docked bottom-right. This
change makes it full-height, auto-hiding, with a small "chip" window left at
the screen edge while hidden. WinUI 3 / Windows App SDK 2.1.3, unpackaged.

## Goals / Non-Goals

**Goals:**

- Full work-area height with small top/bottom gaps.
- Panel fully off-screen when not in use; cursor-at-edge brings it back.
- A persistent, glanceable unread affordance while hidden.

**Non-Goals:**

- Multi-monitor awareness (primary work area only, as before).
- Persisting unread state across app restarts.
- Click-to-pin, drag-to-resize, or any chip interactions beyond hover-to-show.

## Decisions

### D1: Separate top-level window for the chip, not a sliver of the panel

First attempt kept the panel partially on-screen (22px) in the hidden state.
Rejected: the panel is full-height, so the leftover sliver was a full-height
acrylic strip — visually loud, exactly what slide-out was meant to remove. A
separate small window can be short, vertically centered, and shown/hidden
independently. Cost: a second `Window` + AppWindow pair and Show/Hide
choreography in `Slide()`/`OnSlideTick()`.

### D2: Cursor polling (60ms `DispatcherTimer` + `GetCursorPos`), not mouse hooks

A low-level mouse hook (`WH_MOUSE_LL`) or `SetWinEventHook` would be
event-driven, but hooks add global-hook liabilities (latency budget in the hook
callback, cleanup on crash) for no felt difference at this scale. Polling at
60ms is imperceptible for a hover affordance and trivially safe. The poll also
drives the auto-hide grace timing (`_leftPanelAtUtc`).

### D3: Slide = timer-driven `AppWindow.Move`, not XAML animation

The panel's position is window geometry, not XAML transform — Storyboards can't
move a top-level window. A 16ms `DispatcherTimer` stepping an ease-out cubic
over 220ms is the whole animation system. `AppWindow.Move` keeps Y fixed at the
cached `_panelY`.

### D4: The chip window is wider than it looks (the ~133px OS clamp)

**The trap that ate most of the debugging time:** Windows clamps top-level
windows to the minimum track width (~133px, `SM_CXMINTRACK`-derived; measured
133px at 96dpi). `AppWindow.MoveAndResize` to 28px silently produces a 133px
window. `OverlappedPresenter.PreferredMinimumWidth = 1` did not lift the clamp,
and `SetWindowPos` with `SWP_NOSENDCHANGING` produced a 0-width window.

Decision: stop fighting the clamp. The window is positioned so its **left
28dip** sit on screen; the excess hangs harmlessly past the screen edge. All
chip content is **left-anchored** in XAML (a 28dip-wide `Border`,
`HorizontalAlignment="Left"`) — never centered in the window, because the
window's center is off-screen. Verified with `GetWindowRect`: left edge at
`workAreaRight − 28dip`.

### D5: Corner rounding — DWM preference for the panel, `SetWindowRgn` for the chip

Borderless windows (`SetBorderAndTitleBar(false, false)`) lose Win11's default
rounding, so the panel explicitly requests `DWMWA_WINDOW_CORNER_PREFERENCE =
DWMWCP_ROUND`; the XAML content clip drops 13px → 8px to match DWM's radius.
The chip can't use DWM rounding (it would round the right edge mid-screen…
and empirically didn't apply to the tiny window at all), so it's shaped with
`SetWindowRgn` — a round-rect region whose right corners deliberately fall past
the screen edge, leaving a left-rounded tab. Note `SetWindowRgn` was wrongly
blamed for the invisible-count bug during debugging; the real cause was D4.
XAML `CornerRadius` alone was rejected for the chip because the corner pixels
outside the rounded border show the opaque window background.

### D6: Unread = counter in the panel, reset by a 3s "seen" timer

`_unreadNudges` increments wherever nudges are inserted (scheduler tick and
Run-Now) unless `_panelSeenForWhile` is set. A one-shot 3s `DispatcherTimer`
starts on slide-in (and on launch, since the panel starts visible); when it
fires, unread resets and the flag is set until the next slide-out. No
persistence — restart starts at 0 (see Non-Goals).

## Risks / Trade-offs

- [Polling loop runs forever at 60ms] → negligible cost (one `GetCursorPos` +
  comparisons); revisit only if Huddle grows a power-saving mode.
- [OS min-track-width is a magic ~133px] → we never depend on the actual value,
  only on "left 28dip are on screen"; geometry is derived from our own
  constants.
- [`PreferredMinimumWidth` ineffective on WinAppSDK 2.1.3] → documented here;
  if a future SDK honors it, the left-anchoring still works unchanged.
- [Chip is hover-only] → a click on the chip does nothing; fine for now, noted
  as a likely future affordance (D2-style omission).
