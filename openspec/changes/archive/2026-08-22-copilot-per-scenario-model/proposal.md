## Why

Model names are provider-specific: the `claude` CLI uses aliases (`opus`/`sonnet`/`haiku`); the Copilot CLI uses full names (`claude-opus-5`, …) and **rejects** the Claude aliases ("Model 'opus' … is not available"). The Copilot provider ignored the per-scenario `model` entirely and always used the top-level config model — so a custom scenario couldn't choose its own model on Copilot, even though Copilot's `--model` accepts real model names.

## What Changes

- The Copilot/Agency provider resolves the model **provider-relatively**:
  - Use the scenario's own `model` when it is a Copilot-native name (e.g. `claude-opus-5`) → **per-scenario model control on Copilot**.
  - Fall back to the configured top-level `model` when the scenario's `model` is a bare Claude alias (`opus`/`sonnet`/`haiku`) or blank — the built-in scenarios and the config default use those aliases, and Copilot rejects them.
- README documents the provider-relative `model` behavior.

## Capabilities

### Modified Capabilities

- `scenario-backend`: the Copilot provider selects the model per-scenario (a Copilot-native `model` is passed through) with a fall back to the configured top-level model for bare Claude aliases.

## Impact

- **Code:** `CopilotCliProvider.EffectiveModel` (new) used in `CompleteAsync`; README. No change to the Claude provider, load-time validation, or the built-ins.
- **Backward compatible:** built-ins and alias-default scenarios still run on the same top-level Copilot model as before; only Copilot-native per-scenario names are newly honored.
