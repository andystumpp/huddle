# Peek panel slide UX — full height, edge slide, unread chip

> **Retrofit note:** this change documents work already implemented and committed on
> branch `peek-panel-slide-ux` (commit `556c085`). Artifacts were written after the
> fact to keep the spec trail honest; tasks.md records what was done and how it was
> verified.

## Why

The docked 460dip panel wasted most of the screen's height and sat permanently on
top of other windows, demanding manual management. The panel should use the full
height when open, get fully out of the way when not needed, and still leave a
small persistent affordance so the user neither forgets the app exists nor misses
new nudges.

## What Changes

- Panel stretches to the full work-area height with 12dip top/bottom gaps
  (previously a fixed 460dip box anchored bottom-right).
- Panel auto-hides: it slides fully off-screen (220ms ease-out) after the cursor
  has been off the panel for 700ms, and slides back in when the cursor hovers
  the chip at the right screen edge.
- New peek chip: a separate small always-on-top window (28dip visible width,
  168dip tall, vertically centered on the right edge) shown only while the panel
  is hidden. It displays the unread nudge count with a pulsing halo when > 0.
- Unread semantics: nudges arriving while the panel is hidden (or not yet "seen")
  increment the chip count; the count resets to 0 once the panel has stayed open
  for 3 seconds.
- Rounded corners everywhere: the panel window requests DWM round corners
  (borderless windows lose Win11's default rounding) and its content clip drops
  from 13px to 8px to match; the chip is shaped via `SetWindowRgn`.

## Capabilities

### New Capabilities

(none — all changes land in the existing shell surface)

### Modified Capabilities

- `app-shell`: the "Peek panel placement and chrome" requirement changes (full
  work-area height, 8px corners, recompute-on-show now also caches slide
  geometry), and three requirements are added: panel slide-out/slide-in,
  the peek chip window, and unread-count tracking.

## Impact

- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — positioning, slide animation,
  cursor-poll hover detection, unread tracking, DWM corner preference.
- `src/Huddle.App/Views/PeekPanelWindow.xaml` — content clip radius 13 → 8.
- `src/Huddle.App/Views/PeekTabWindow.xaml(.cs)` — new chip window.
- No storage, capture, or scenario changes. No new dependencies.
