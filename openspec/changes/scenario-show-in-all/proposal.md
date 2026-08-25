## Why

The Nudges tab's `All` view shows every scenario's nudges, and it got crowded. Some scenarios — Achievements is the example — are worth keeping but not worth seeing in the default aggregate; the user wants them out of `All` while still reachable. And if a scenario is hidden from the default view, a new one of its nudges shouldn't demand attention either.

## What Changes

- A per-scenario config field `showInAll` (boolean, default **true**) controls whether the scenario's nudges appear in the `All` view.
- When `showInAll` is `false`: the scenario's nudges are excluded from `All`, but its own filter pill still shows all of them. The pill itself is unaffected — every configured scenario keeps its pill.
- A nudge from a `showInAll: false` scenario does **not** increment the unread count or pulse the chip halo — hidden from the default view means silent by default.
- Default (unset) is `true`, so every existing config and scenario is unchanged.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `scenario-config`: the inline scenario definition gains an optional `showInAll` field (default true).
- `app-shell`: the Nudges tab `All` filter excludes `showInAll: false` scenarios (their own pill still shows them), and the chip's unread count ignores their nudges.

## Impact

- `src/Huddle.App/Config/ScenarioConfig.cs` + `HuddleConfig.cs` — add `ShowInAll` (default true); a small `Bool(name, fallback)` parse helper.
- `src/Huddle.App/Scenarios/Scenario.cs` + `ConfiguredScenario.cs` — expose `ShowInAll` (default true; override from the def).
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — `RebuildNudgeDisplay` skips `showInAll: false` scenarios in the `All` branch; the two `_unreadNudges++` sites guard on `ShowInAll`.
- `huddle.config.example.json` — document the field; set Achievements to `"showInAll": false` as the default example.
