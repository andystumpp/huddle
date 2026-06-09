## Why

The tray-only shell was the right long-term shape (per ADR 0001) but it's overkill for this stage of the build. We want to see and touch the panel right away, and a tray icon adds a layer of plumbing (icon resource, `H.NotifyIcon`, anti-flicker on click) that we don't need yet. A plain taskbar app — one window that opens when you launch it — is the smallest thing that lets us iterate on the panel's visuals.

## What Changes

- **BREAKING:** Remove the tray icon entirely. The app no longer registers a system tray icon.
- **BREAKING:** Remove the tray context menu (Open / Quit).
- **BREAKING:** Remove the lost-activation / click-outside auto-hide. The window stays open until the user closes it.
- **BREAKING:** Remove the 250 ms anti-flicker tray-click suppression.
- The app SHALL launch with the peek panel window already visible, with a Huddle entry in the taskbar.
- Closing the panel window (via the close affordance or Alt+F4) SHALL exit the app.
- The panel keeps its existing chrome: 384 px wide, docked 12 px from the bottom-right of the primary display's work area, 13 px rounded corners, DesktopAcrylic, and the brand-block content (Huddle mark + "Huddle").
- The window is no longer borderless — it has a thin native title bar so it can be moved, minimized, restored, and closed from the taskbar like any normal window. (We'll revisit chrome customization later.)
- Drop the `H.NotifyIcon.WinUI` dependency.

## Capabilities

### New Capabilities
- `app-shell`: (continues from `add-app-shell-peek-panel`) — same capability, with the tray-only behavior replaced by a standard taskbar window.

### Modified Capabilities

_None as a synced spec yet — `add-app-shell-peek-panel` is still unarchived, so this change writes the new state directly into the `app-shell` spec. When both changes are archived, the result is the spec in this change._

## Impact

- `src/Huddle.App/App.xaml.cs` — strip tray icon setup and the `OnTrayLeftClick` anti-flicker handler; just show the panel window on launch.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — drop the `WS_EX_TOOLWINDOW` style (so a taskbar entry appears), drop the borderless presenter config, drop `Activated`-based hide and the Esc-to-hide handler, drop `LastDeactivatedAt`, drop `IsPanelVisible` bookkeeping; keep acrylic, positioning, and topmost-on-show.
- `src/Huddle.App/RelayCommand.cs` — no longer needed; remove.
- `src/Huddle.App/Huddle.App.csproj` — remove the `H.NotifyIcon.WinUI` package reference.
- No data, storage, or pipeline impact.
