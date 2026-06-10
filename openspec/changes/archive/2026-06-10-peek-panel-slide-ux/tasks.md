# Tasks — peek panel slide UX

> Retrofit: implementation already landed in commit `556c085` on branch
> `peek-panel-slide-ux`; boxes are checked to reflect completed work.

## 1. Full-height panel

- [x] 1.1 Replace fixed 460dip height with work-area height minus 12dip top/bottom gaps (320dip minimum) in `PositionPanel`
- [x] 1.2 Cache slide geometry (`_visibleX`, `_hiddenX`, `_panelY`, work-area right) during `PositionPanel`

## 2. Slide-out / slide-in

- [x] 2.1 Hover watch: 60ms `DispatcherTimer` polling `GetCursorPos`
- [x] 2.2 Auto-hide after 700ms cursor-off grace; cursor return cancels
- [x] 2.3 Slide animation: 16ms timer stepping ease-out cubic over 220ms via `AppWindow.Move`
- [x] 2.4 Slide-in triggered by cursor entering the chip hover zone (chip rect + 8px forgiveness)

## 3. Peek chip window

- [x] 3.1 New `PeekTabWindow` (28dip visible width, 168dip tall, borderless, always-on-top)
- [x] 3.2 Left-anchor all chip content in a 28dip Border (OS clamps window to ~133px min width; center of window is off-screen)
- [x] 3.3 Shape chip via `SetWindowRgn` round-rect with right corners past the screen edge
- [x] 3.4 Show chip when slide-out completes; hide it when slide-in starts; first Show/Hide cycle at startup so the visual tree realizes

## 4. Unread count

- [x] 4.1 `_unreadNudges` increments on nudge insert (scheduler tick + Run Now) while panel not seen
- [x] 4.2 3s read-grace timer on slide-in (and launch) resets unread to 0
- [x] 4.3 Pulsing halo storyboard on the chip while unread > 0

## 5. Rounded corners

- [x] 5.1 Panel requests `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`
- [x] 5.2 Content clip radius 13 → 8 to match DWM's curve

## 6. Verification

- [x] 6.1 `GetWindowRect` (PowerShell): panel hidden at `x = workAreaRight` (fully off-screen), chip's left edge at `workAreaRight − 28dip` with exactly 28px on screen despite the 133px clamped window width
- [x] 6.2 Manual: panel stretches full height with 12dip gaps; slides out after ~700ms cursor-off; chip hover slides it back in
- [x] 6.3 Manual: chip count visible, pulses when unread > 0, resets to 0 after panel open ≥ 3s; quick peek (< 3s) preserves count
- [x] 6.4 Manual (screenshot): all four panel corners rounded, acrylic included; chip renders as left-rounded tab
