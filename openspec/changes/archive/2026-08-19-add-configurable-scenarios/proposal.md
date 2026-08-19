## Why

Huddle's scenarios are compiled in: `ScenarioRegistry` is a hardcoded list of four classes with their prompts baked in. Running Huddle in different contexts — a personal machine versus a work laptop — needs a different scenario set (turn some off, add context-specific ones such as a "value delivery" coach) without a rebuild. The registry already anticipates this (`// Hardcoded for now — when the .md plugin loader lands, replace the initializer`); this change delivers config-driven scenarios, reusing the same non-secret `huddle.config.json` that already selects the provider and capture scope.

## What Changes

- Add an optional `scenarios` section to `huddle.config.json`:
  - `disabled`: a list of built-in scenario keys to turn off on this machine.
  - `custom`: inline scenario definitions.
- A **custom scenario definition** carries: `key`, `displayName`, `accentColorHex`, `cadenceHours`, `trailSize`, `priorNudgesSize`, `model` (alias string, e.g. `opus`/`sonnet`), optional `effort` (`low`|`medium`|`high`|`xhigh`|`max`), `webSearch` (bool), and `systemPrompt` (the prompt text).
- A single generic `ConfiguredScenario` runs each custom scenario through the **existing** trail → `Provider.CompleteAsync` → `NudgeDraft` template. The pipeline still appends the `NudgeDraft` JSON-schema directive, so a config-authored `systemPrompt` only has to describe **when to emit and the voice** — never the JSON shape.
- `ScenarioRegistry` becomes a composed set: **built-ins minus `disabled`, plus valid `custom` entries**.
- **Backward compatible:** no `scenarios` section → the current four built-ins (LinkedIn, Achievements, Learnings, Efficiency) run exactly as today.
- Invalid custom entries (missing `key`/`systemPrompt`, a `key` that collides with a built-in or another custom, or a bad `model`/`effort` value) are **skipped with a logged warning**; the rest still run.
- README documents the `scenarios` shape and ships a **value-delivery** example config — a work scenario that watches the trail and surfaces one concrete, higher-leverage way to deliver or demonstrate value (forward-looking, distinct from Achievements). The example carries a short **placeholder** `systemPrompt`; the real prompt is authored and tuned on the target (work) machine.

Non-goals (v1): overriding a built-in's prompt (disable it and add a custom instead), per-scenario provider selection, loading prompts from external `.md` files (config-inline only), and a scenarios UI.

## Capabilities

### New Capabilities

- `scenario-config`: how the active scenario set is assembled from built-ins plus configuration (disable built-ins by key; define custom scenarios inline) and how a config-defined scenario executes through the shared scenario pipeline.

### Modified Capabilities

<!-- None. Provider dispatch (scenario-backend) and the nudge shape (nudges) are unchanged — custom scenarios reuse them as-is. -->

## Impact

- **Code:** `HuddleConfig` gains a parsed `scenarios` section (new `ScenarioConfig` / `CustomScenarioDef` types); new `ConfiguredScenario : Scenario`; `ScenarioRegistry.All` changes from a `static readonly` array to a set composed from built-ins ∖ disabled ∪ custom. The `Scenario` base already exposes every virtual a custom scenario needs (`Key`, `DisplayName`, `AccentColorHex`, `Cadence`, `TrailSize`, `PriorNudgesSize`, `ModelId`, and the `Provider`).
- **Config:** `huddle.config.json` gains an optional, non-secret `scenarios` object (gitignored like the rest of the file).
- **Docs:** README configuration section.
- **No** database/schema change (nudges keep their shape), and **no** provider/dispatch change.
