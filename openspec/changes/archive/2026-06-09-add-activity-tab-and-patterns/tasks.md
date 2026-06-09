## 1. Data model + seed

- [x] 1.1 Add `src/Huddle.App/Models/Pattern.cs` — `record Pattern(string Id, string Title, string Description, IReadOnlyList<string> SourceApps)`
- [x] 1.2 Add `src/Huddle.App/Models/PatternSeed.cs` exposing `static readonly IReadOnlyList<Pattern> All` with the four patterns from `design.md` D2 (Heavy context-switching, Wrestling one sentence, Repeating yourself, plus one extra pattern for the fold)

## 2. AppTile control

- [x] 2.1 Add `src/Huddle.App/Controls/AppTile.xaml(.cs)` — `UserControl` with `AppKey` (string) and `Size` (int, default 22) dependency properties
- [x] 2.2 Hard-code the monogram + tint table inside the control (mirrors `design/huddle/project/huddle/data.jsx` `APP_META`): `Code.exe` → `VS` / `#3C9DF0`, `Chrome` → `Cr` / `#E8534B`, `Notepad` → `Nt` / `#8AA0B4`, `Slack` → `Sl` / `#C4A1E8`, `Windows Terminal` → `>_` / `#4ED6A8`
- [x] 2.3 Render a tinted rounded square with the 2-char monogram centered, font-size = size * 0.42

## 3. PatternCard control

- [x] 3.1 Add `src/Huddle.App/Controls/PatternCard.xaml(.cs)` — `UserControl` with a `Pattern` dependency property
- [x] 3.2 Layout: a single rounded card (10 px corner radius), background `#0BFFFFFF`, 1 px border `#12FFFFFF`, padding `13`
- [x] 3.3 Content: bold one-line title (`FontWeight=SemiBold`, `FontSize=14`, color `#F0FFFFFF`), description below (`FontSize=12.5`, line-height `1.45`, color `#C8FFFFFF`)
- [x] 3.4 Footer row: horizontal stack of `AppTile`s (one per source app, 7 px gap)

## 4. Tab strip

- [x] 4.1 In `PeekPanelWindow.xaml`, remove the existing filter-chip `StackPanel` (the chips that live in the filters row of the current grid)
- [x] 4.2 Add a two-column `Grid` in its place with two `ToggleButton` tabs — Nudges (lightbulb glyph) and Activity (pulse/activity glyph), each with a label + count, equal width with 6 px gap
- [x] 4.3 Add a new `TabStyle` resource: 44 px high, 8 px corner radius, muted background when unselected, brighter background + a left accent border (using the social violet for Nudges, a pink/coral aurora-accent for Activity to match the design) when selected, full-opacity label when selected
- [x] 4.4 In `PeekPanelWindow.xaml.cs`, remove `OnChipClick`; add `OnTabClick` that forces the clicked tab checked and the other unchecked (clicking the already-selected tab keeps it selected)
- [x] 4.5 On launch, set the Activity tab as the default selected

## 5. Activity content + tab swap

- [x] 5.1 In the stream area (row 3 of the main grid), wrap the existing empty state in a `Grid` named `NudgesContent` and bind its `Visibility` to a code-behind property `bool IsNudgesTabSelected`
- [x] 5.2 Add a sibling `Grid` named `ActivityContent` containing the section header and a `ScrollViewer` hosting an `ItemsRepeater` of patterns; bind its `Visibility` inversely (visible when Activity tab is selected)
- [x] 5.3 Section header: a small circled-plus glyph + "PATTERNS DETECTED" uppercase letter-spaced + count text. Padding `14,14,14,8`
- [x] 5.4 `ItemsRepeater` `ItemTemplate` instantiates `PatternCard` with its `Pattern` property bound to the item; vertical stack layout with 9 px gap; container padded `14,0,14,14`
- [x] 5.5 Source the items from `PatternSeed.All` (in the order provided)

## 6. Tab count wiring

- [x] 6.1 Bind the Nudges tab count to `0` (literal for now — no nudges yet)
- [x] 6.2 Bind the Activity tab count to `PatternSeed.All.Count`

## 7. Verification

- [x] 7.1 `dotnet build Huddle.slnx -c Debug` succeeds with 0 warnings, 0 errors
- [x] 7.2 Launch — the panel opens with the Activity tab selected by default
- [x] 7.3 The Activity tab shows "PATTERNS DETECTED 4" and four pattern cards
- [x] 7.4 Each pattern card shows title, description, and source-app monogram tiles
- [x] 7.5 Clicking the Nudges tab swaps to the empty state ("No nudges right now.")
- [x] 7.6 Clicking the Activity tab swaps back to the patterns list
- [x] 7.7 Clicking the already-selected tab keeps it selected
