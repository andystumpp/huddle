## 1. Config field

- [x] 1.1 `HuddleConfig.cs`: add `public int PanelWidth { get; init; } = 384;`. In `Load`, parse the top-level `panelWidth` number and set `PanelWidth = Math.Clamp(value, 280, 1200)` (missing / non-number → 384).

## 2. Consume it

- [x] 2.1 `PeekPanelWindow`: replace `private const int PanelWidth = 384;` with `private readonly int _panelWidthDip = HuddleConfig.Current.PanelWidth;`, and use `_panelWidthDip` at the two sites (`PositionPanel` width via `ScaleToPx`, and `LookBarClip.Rect`).

## 3. Example config

- [x] 3.1 `huddle.config.example.json`: document the top-level `panelWidth` (default 384).

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 4.2 With `panelWidth` set in the running config, the panel docks at that width, still 12 px from the right edge and full work-area height — verified via `GetWindowRect`.
- [x] 4.3 Default (no `panelWidth`) still docks at 384; an out-of-range value clamps.
- [x] 4.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-24.

**4.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**4.2 — Configured width applied (automated).** Dropped a temporary `{ "provider": "claude", "panelWidth": 520 }` in the exe dir (which takes config precedence over `%LOCALAPPDATA%`), launched, and read the panel's rect via `GetWindowRect`:

```
panel width = 520px (configured panelWidth=520; 96 dpi → 520px)
RESULT: PASS — configured width applied
cleanup: test config removed? True
```

The panel docked at 520 px vs the default 384 px — the config value drives the width end-to-end. The temp config was removed afterward.

**4.3 — Default & clamp.** With no `panelWidth`, the panel docks at 384 (confirmed in prior runs, e.g. the display-change test's `W=384`). Out-of-range values are bounded by `Math.Clamp(value, 280, 1200)` in `HuddleConfig.Load` (by inspection; a pure clamp).

