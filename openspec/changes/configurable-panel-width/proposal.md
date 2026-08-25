## Why

The peek panel's width is a hardcoded `const int PanelWidth = 384`. On a large monitor a wider panel is nicer; on a small laptop a narrower one intrudes less. The width should be settable in config like the other presentation knobs, without a code change.

## What Changes

- A top-level config field `panelWidth` (pixels, DIPs) sets the panel's width. Default **384**, clamped to a sane range (280–1200) so a typo can't produce an unusable window.
- `PositionPanel` and the look-bar clip read the configured width instead of the constant. Everything else about docking (gaps, height, slide geometry) is unchanged, so a wider panel still docks 12 px from the right edge and stretches the work-area height.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `app-shell`: the *Peek panel placement and chrome* requirement's fixed 384 px width becomes a configurable width (default 384).

## Impact

- `src/Huddle.App/Config/HuddleConfig.cs` — add `PanelWidth` (default 384) parsed from the top-level `panelWidth`, clamped to 280–1200.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — replace the `PanelWidth` constant with the configured value at its two use sites (`PositionPanel` width, look-bar clip).
- `huddle.config.example.json` — document the field.
