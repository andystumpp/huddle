## 1. Data: 7-day window

- [x] 1.1 Add `NudgeStore.SinceAsync(DateTimeOffset cutoff)` — `SELECT … WHERE ts >= $cutoff ORDER BY ts DESC`, reusing the existing `Read` helper.

## 2. Display model + template selector

- [x] 2.1 Add `Views/NudgeListItem.cs`: a `NudgeDayHeader(string Label)` record and a `NudgeListItemTemplateSelector : DataTemplateSelector` (HeaderTemplate / NudgeTemplate) that picks by runtime type.

## 3. XAML: chips, templates, selector

- [x] 3.1 Add `xmlns:local` and resources to `PeekPanelWindow.xaml`: the day-header `DataTemplate`, the `NudgeCard` `DataTemplate`, the selector, and a `FilterChipStyle` (mirrors `TabStyle`).
- [x] 3.2 Add a filter chip row to the Nudges tab (`All · Achievements · Learnings · Posts · Efficiency`, single-select, `All` checked) in a horizontally-scrollable strip.
- [x] 3.3 Point `NudgesRepeater.ItemTemplate` at the selector; add a `RowDefinition` for the chips.

## 4. Code-behind: load, group, filter

- [x] 4.1 Replace `_nudges` (flat, capped at 20) with `_allNudges` (7-day backing list) + `_nudgeDisplay` (`ObservableCollection<object>`); bind the repeater to `_nudgeDisplay`.
- [x] 4.2 Load via `NudgeStore.SinceAsync(UtcNow - 7d)` in `OnContentLoaded`.
- [x] 4.3 Add `RebuildNudgeDisplay()` (filter by `_activeScenarioFilter`, group by local day, emit `NudgeDayHeader` + `Nudge`) and `DayLabel()` (`TODAY`/`YESTERDAY`/`ddd · MMM d`).
- [x] 4.4 Add `OnFilterChipClick` (single-select, set filter, rebuild). New nudges (tick + Run-now) insert at `_allNudges[0]` and rebuild.
- [x] 4.5 Update `UpdateNudgesSurface` to key the empty state off `_nudgeDisplay` and the counts off `_allNudges`.

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` is clean.
- [x] 5.2 Launched; Nudges tab shows day headers (TODAY/YESTERDAY/date) and the filter chips; tapping a scenario chip filters and re-groups. Visually reviewed and approved.

## Verification

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Runtime** — Relaunched Huddle over the real 7-day nudge window. The Nudges tab renders day-grouped headers with `NudgeCard`s beneath, and the scenario chip row filters + re-groups. Confirmed visually with the user (approved). The Activity/moments tab, `NudgeCard`, and scenario/backend behavior are unchanged.
