## Context

The Nudges tab bound its `ItemsRepeater` directly to `_nudges` — an `ObservableCollection<Nudge>` holding the most-recent 20, loaded via `NudgeStore.RecentAsync(20)`. It rendered one `NudgeCard` per item, flat, across all scenarios and all time. With ~89 nudges accumulated, that made a given day's work impossible to isolate.

This change reworks only the Nudges tab into a day-grouped, scenario-filterable review. The `NudgeCard`, the Activity/moments tab, and all scenario/backend code are untouched. Export is deliberately out of scope (it belongs to the later content-pipeline work).

## Goals / Non-Goals

**Goals:**
- Make a day's (or a scenario's) nudges reviewable at a glance.
- Reuse the existing `NudgeCard` and `ItemsRepeater`; keep the change contained to the Nudges tab.
- Keep live updates working (a new nudge appears under `TODAY` without a reload).

**Non-Goals:**
- Export / copy-to-markdown (separate iteration).
- Grouping the Activity/moments tab.
- Any change to how nudges are produced or stored (schema unchanged).

## Sequence

Two flows share one rebuild step: **Load** (open the tab over a 7-day window) and **Filter** (chip click). Both feed `RebuildNudgeDisplay`, which produces the grouped, filtered display list the repeater renders through a template selector.

```mermaid
sequenceDiagram
    participant UI as PeekPanelWindow
    participant Store as NudgeStore
    participant Disp as _nudgeDisplay (ObservableCollection<object>)
    participant Rep as NudgesRepeater + NudgeListItemTemplateSelector

    rect rgb(245,245,245)
    Note over UI,Store: 1. Load the 7-day window
    UI->>Store: SinceAsync(UtcNow - 7d)
    Store-->>UI: IReadOnlyList<Nudge> (newest first)
    UI->>UI: _allNudges.AddRange(...)
    UI->>UI: RebuildNudgeDisplay()
    end

    rect rgb(245,245,245)
    Note over UI: 2. Filter (chip click)
    UI->>UI: OnFilterChipClick → single-select chips; _activeScenarioFilter = tag|null
    UI->>UI: RebuildNudgeDisplay()
    end

    rect rgb(245,245,245)
    Note over UI,Disp: 3. Rebuild the display list
    UI->>Disp: Clear()
    loop _allNudges (newest first), filtered by scenario
        UI->>Disp: Add(NudgeDayHeader) when the local day changes
        UI->>Disp: Add(Nudge)
    end
    UI->>UI: UpdateNudgesSurface() (empty state, counts)
    end

    rect rgb(245,245,245)
    Note over Disp,Rep: 4. Render (selector picks per item)
    Disp-->>Rep: item is NudgeDayHeader → header template
    Disp-->>Rep: item is Nudge → NudgeCard (unchanged)
    end
```

### 1. Load the 7-day window

**Contract** — In: `cutoff = DateTimeOffset.UtcNow - 7d`. Out: `IReadOnlyList<Nudge>` newest-first. `NudgeStore.SinceAsync` runs `SELECT … WHERE ts >= $cutoff ORDER BY ts DESC` (ts is ISO-8601 UTC, so lexical compare == chronological). The result seeds `_allNudges` (the backing list; the source of truth for both grouping and filtering).

**How** — Replaces the old `RecentAsync(20)` call in `OnContentLoaded`. `_allNudges` is a plain `List<Nudge>` kept newest-first; the display list is derived from it, never the reverse.

### 2. Filter

**Contract** — In: a chip's `Tag` (`""` for All, else a scenario key). Out: `_activeScenarioFilter` (`string?`, null = All) and a single active chip. The chip row offers `All` + one chip per scenario.

**How** — `OnFilterChipClick` forces single-select (sets `IsChecked` true on the clicked chip, false on the rest), maps the tag to `_activeScenarioFilter`, and calls `RebuildNudgeDisplay`.

### 3. Rebuild the display list

**Contract** — In: `_allNudges` (newest-first) + `_activeScenarioFilter`. Out: `_nudgeDisplay`, an `ObservableCollection<object>` of interleaved `NudgeDayHeader` and `Nudge` items, in render order (a header immediately precedes its day's nudges). `NudgeDayHeader = { string Label }`.

**How** — Clear `_nudgeDisplay`; iterate `_allNudges`, skipping items whose `Scenario` doesn't match the active filter; when the local day (`Ts.ToLocalTime().Date`) changes, push a `NudgeDayHeader` labeled by `DayLabel(day, today)` (`TODAY` / `YESTERDAY` / `ddd · MMM d`); then push the `Nudge`. Finish with `UpdateNudgesSurface` (empty state on/off, tab + section counts = `_allNudges.Count`). Cheap enough to run on every filter change and every new nudge. New nudges from the tick / Run-now insert at `_allNudges[0]` and call `RebuildNudgeDisplay`, so they land at the top of `TODAY`.

### 4. Render

**Contract** — In: each `_nudgeDisplay` item. Out: the matching `DataTemplate`. `NudgeListItemTemplateSelector.SelectTemplateCore` returns the header template for a `NudgeDayHeader` and the card template (the unchanged `NudgeCard`) otherwise.

**How** — `NudgesRepeater.ItemTemplate` is the selector resource. The header template binds `{Binding Label}`; the card template hosts `controls:NudgeCard Nudge="{Binding}"`. Because the selector keys off runtime type, one `ItemsRepeater` renders the mixed list with no per-item branching in code.

## Decisions

### D1: Heterogeneous list + template selector, not a grouped control

WinUI's `ItemsRepeater` has no built-in grouping. Rather than swap to `ListView` groups (heavier, different styling), keep the repeater and feed it a flat `object` list of headers + nudges, disambiguated by a `DataTemplateSelector`. Minimal surface, reuses the existing card and layout.

### D2: `_allNudges` backing list + derived display

The 7-day window is the source of truth; `_nudgeDisplay` is a pure projection rebuilt on demand. Filtering and grouping never mutate the backing data, so switching filters is lossless and a full rebuild (Clear + re-add) is simplest and correct for these list sizes.

### D3: 7-day window, client-side grouping

A week matches a blog/LinkedIn cadence and keeps the list scrollable. Grouping/labeling is done client-side (trivial for the volume) rather than in SQL, so `NudgeStore` stays a plain date-bounded query.

## Risks / Trade-offs

- **[Full rebuild loses scroll position on filter change]** → acceptable: a filter change is an intentional context switch, and the lists are small.
- **[Filtered-empty shows the generic empty state]** → the "No nudges right now" state also covers "no nudges for this filter"; acceptable for v1, could get filter-specific copy later.
- **[Window grows unbounded within a session]** → new nudges append to `_allNudges` without a cap, but a session's emissions are few and a reload re-bounds to 7 days.

## Open Questions

- Whether the Activity/moments tab should get the same day grouping later (out of scope now).
