## Why

The Copilot CLI can run a **custom agent** (`--agent <name>` — confirmed on an installed `copilot`). A user who builds an agent for Copilot/Agency should be able to point a scenario at it, the same way scenarios already choose a `model` and `effort`. Today there's no way to select an agent per scenario.

## What Changes

- A per-scenario `agent` config field (string, optional). When set, the Copilot/Agency provider adds `--agent <name>` to the invocation, **additive** with the existing `--model`/`--effort` args.
- Applies to **Copilot and Agency** (Agency reuses `CopilotCliProvider`). The **Claude** provider ignores the field (its CLI has a different agent model) — a no-op, not an error.
- `ScenarioRequest` carries an optional `Agent`; `ConfiguredScenario` sets it from the scenario def. Default unset preserves current behavior for every scenario.

The user owns the agent definition (in their own Copilot setup) and is responsible for building it to cooperate with the structured-output contract — the scenario still expects a `NudgeDraft` JSON object back.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `scenario-config`: the inline scenario definition gains an optional `agent` field.
- `scenario-backend`: the Copilot/Agency provider passes `--agent` when the request carries an agent name.

## Impact

- `src/Huddle.App/Config/ScenarioConfig.cs` + `HuddleConfig.cs` — add optional `Agent` (default null), parsed from `agent`.
- `src/Huddle.App/Scenarios/ICliProvider.cs` — `ScenarioRequest` gains an optional `Agent`.
- `src/Huddle.App/Scenarios/ConfiguredScenario.cs` — set `Agent` on the request from `_def.Agent`.
- `src/Huddle.App/Scenarios/CopilotCliProvider.cs` — when `request.Agent` is set, add `--agent <name>`.
- `huddle.config.example.json` — document the field.
