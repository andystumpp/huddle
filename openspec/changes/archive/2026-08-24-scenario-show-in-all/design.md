# Design — showInAll

One boolean, read at two gates: the `All` display list and the unread count. Both consult the same predicate — "does this scenario show in All?" — resolved from config via the registry.

## Where the gate applies

```mermaid
flowchart TD
    N[Scenario emits a Nudge] --> A[NudgeStore.AddAsync + insert into _allNudges]
    A --> U{ShowInAll(scenario)?}
    U -- yes --> INC["_unreadNudges++ (if panel unseen)<br/>→ chip count + halo"]
    U -- no --> SKIP[unread unchanged — silent]
    A --> R[RebuildNudgeDisplay]
    R --> F{active filter}
    F -- a scenario pill --> ONE[show only that scenario<br/>ShowInAll ignored]
    F -- All --> G{ShowInAll(scenario)?}
    G -- yes --> SHOW[include in All]
    G -- no --> HIDE[exclude from All]
```

**Contract**

- `Scenario.ShowInAll` — `bool`, default `true`. `ConfiguredScenario` overrides it from `ScenarioDef.ShowInAll`, parsed from the config's `showInAll` (absent → `true`; only explicit `false` disables).
- `ShowInAll(scenarioKey)` helper in `PeekPanelWindow` → `ScenarioRegistry.GetByKey(key)?.ShowInAll ?? true` (an unknown scenario defaults to shown, matching the tag fallback).
- **All-view gate** (`RebuildNudgeDisplay`) — the existing filter test becomes: when `_activeScenarioFilter is null`, include a nudge only if `ShowInAll(n.Scenario)`; when a specific scenario is active, the test is unchanged (`n.Scenario == _activeScenarioFilter`), so a hidden scenario is fully visible under its own pill.
- **Unread gate** — both `_unreadNudges++` sites (the tick `RunScenariosAsync` and the manual "Run now") guard on `ShowInAll(result.Nudge.Scenario)`. Because the chip's pulsing halo is driven by the unread count, keeping the count at zero also keeps the halo still — no separate pulse handling.

## Why the registry, not the nudge

A `Nudge` carries only its scenario `key`, not presentation config. `showInAll` is a scenario setting, so it's resolved through `ScenarioRegistry.GetByKey` — the same path the card already uses for the display name and accent color. Nudges already in the store need no migration: the flag is applied at render/count time, so flipping `showInAll` in config takes effect on the next rebuild.

## Deliberately omitted

- **Hiding the pill.** `showInAll` only affects the aggregate `All` list and the unread count; every scenario keeps its filter pill, which is how a hidden scenario stays reachable.
- **A separate "notify but hide" mode.** Hidden-from-All is silent by decision; a scenario that should still ping simply keeps `showInAll: true`.
- **Total-count change.** The header's total nudge count stays the lifetime total; only the *unread* count (the attention signal) honors `showInAll`.
