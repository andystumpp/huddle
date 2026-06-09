## Context

The previous change (`add-app-shell-peek-panel`) built a tray-only WinUI 3 shell with a borderless, click-outside-to-dismiss peek panel docked at the bottom-right. It runs, but the tray surface adds friction we don't need right now: an icon resource has to be wired, `H.NotifyIcon` brings its own activation quirks, and the anti-flicker dance around tray-click vs. lost-activation hides has edge cases.

For this stage, we just want a normal Windows app: launch it, the panel window appears, it shows up in the taskbar, you close it when you're done. The panel's visuals (acrylic, rounded corners, dock position, brand block) stay exactly as they are.

## Goals / Non-Goals

**Goals:**
- Launching `Huddle.exe` shows the peek panel window immediately.
- A normal Huddle entry appears in the taskbar (icon + title).
- The window has standard native chrome — a thin title bar with close / minimize, draggable, resizable suppression preserved.
- Window stays put: no auto-hide on losing focus, no Esc to close beyond the standard.
- Closing the window exits the app.
- Panel keeps its position (bottom-right of work area, 12 px margins), size (384 px wide), corners (13 px), and DesktopAcrylic backdrop.
- Brand block (Huddle mark + "Huddle") continues to render as the only content.

**Non-Goals:**
- Tray icon, tray context menu, any `H.NotifyIcon` integration — fully removed in this change.
- Customized title bar (no extending content into title bar in this change — defer to a later visual pass).
- Multi-window behavior, system-wide hotkeys, auto-start at login.
- Any nudge / store / pipeline work.

## Decisions

### D1. Drop `H.NotifyIcon.WinUI`

- **Choice:** Remove the package reference and all tray code paths.
- **Rationale:** Nothing else uses it. Smaller dependency surface, simpler startup.

### D2. App.OnLaunched shows the window immediately

- **Choice:** `OnLaunched` constructs `PeekPanelWindow` and calls `ShowPanel()` directly. No tray instantiation, no `RelayCommand`.
- **Rationale:** The window is the entire app now — no reason to start it hidden.

### D3. Window participates in the taskbar

- **Choice:** Stop applying `WS_EX_TOOLWINDOW` to the window's extended style. Without that flag, Windows treats it as a normal top-level window and gives it a taskbar entry. The app icon (`AppIcon.ico`) is already wired via `<ApplicationIcon>` in the csproj, so the taskbar entry will pick it up automatically.
- **Rationale:** Standard behavior, zero extra code.

### D4. Standard chrome, presenter not borderless

- **Choice:** Set `ExtendsContentIntoTitleBar = false` and let the `OverlappedPresenter` keep its default border + title bar. Still disable resize/minimize/maximize to preserve the docked feel (the panel isn't meant to be dragged around). Keep `IsAlwaysOnTop = true` so the panel stays above other windows while open.
- **Rationale:** The thin native title bar is a small visual compromise that buys us close-button, minimize, and a taskbar that "just works." We can replace it with a custom title bar in a later visual pass once we've validated the rest.
- **Alternative:** Keep borderless and add a custom close button — rejected as scope creep for this change.

### D5. Close-to-exit

- **Choice:** Subscribe to `Window.Closed` on the panel and call `Application.Current.Exit()`. With only one window, closing it should terminate the process.
- **Rationale:** Standard single-window app behavior; without it, the message pump can keep running.

### D6. Keep the dock positioning logic

- **Choice:** `PositionPanel()` and the P/Invoke for work-area + DPI stay exactly as they are. It's the right behavior regardless of tray vs. taskbar, and it's already proven against the design.
- **Rationale:** No reason to touch what works.

### D7. Drop the auto-hide and Esc handlers

- **Choice:** Remove the `Activated` handler that hid on deactivation, remove `OnContentKeyDown` Esc-to-hide, remove `IsPanelVisible` and `LastDeactivatedAt`. The panel is just visible until the user closes it.
- **Rationale:** Auto-hide makes sense for a flyout-style tray panel; for a normal window it's surprising and breaks the taskbar interaction model.

## Risks / Trade-offs

- **[The native title bar doesn't match the prototype's borderless aesthetic]** → Accepted. We're trading visual fidelity for development velocity at this stage; the chrome customization is a known follow-up.
- **[Topmost + taskbar entry can feel pushy if the user wants to focus another window in front]** → Mitigation: `IsAlwaysOnTop = true` matches the tray-flyout intent. If it gets in the way during real use we drop topmost — the panel still stays open, just not on top.
- **[Removing `H.NotifyIcon.WinUI` from the csproj invalidates the lockfile / restore cache; first build after the change may be a hair slower]** → Negligible.

## Open Questions

- Do we want a Huddle-mark-shaped taskbar icon eventually, or stay with the generic `AppIcon.ico`? Current plan: generic for now; revisit when the brand work lands.
- When we add the tray back later, do we want both surfaces (tray + taskbar) or replace? Current plan: tray supersedes when it returns. Not decided in this change.
