## Context

Today the scenario set is composed from four hard-coded classes plus optional config additions: `ScenarioRegistry` starts from `new LinkedInPostsScenario()` etc., drops `disabled`, and appends `custom` defs (run via `ConfiguredScenario`). All the tuning that matters — the prompts — lives in code. This change makes configuration the *only* source of scenarios: the four classes are deleted, their prompts move to a shipped example config, and every scenario runs through one config-driven type. What's in the config array is exactly what runs; nothing is baked in.

## Sequence

```mermaid
sequenceDiagram
    participant Cfg as HuddleConfig
    participant Reg as ScenarioRegistry
    participant Tick as Tick loop
    participant Prov as ICliProvider
    participant Store as NudgeStore

    rect rgb(245,245,245)
    Note over Cfg,Reg: 1. Build the set from config (once)
    Cfg->>Cfg: parse scenarios: [ ScenarioDef, … ]
    Cfg-->>Reg: ScenarioDef[]
    Reg->>Reg: validate each; skip invalid (log); dedupe keys
    Reg-->>Tick: IReadOnlyList<Scenario>  (empty if none configured)
    end

    rect rgb(245,245,245)
    Note over Tick,Store: 2. Run a due scenario (unchanged pipeline)
    Tick->>Prov: CompleteAsync(ScenarioRequest from the def)
    Prov-->>Tick: NudgeDraft JSON
    Tick->>Store: NudgeStore.Add(nudge)
    end
```

### 1. Build the set from config

**Contract** — In: `scenarios` as a JSON **array** of scenario definitions (absent/empty → no scenarios). A `ScenarioDef` is `{ key, displayName?, accentColorHex?, cadenceHours?, trailSize?, priorNudgesSize?, model?, effort?, webSearch?, systemPrompt }` (only `key` and `systemPrompt` required; `systemPrompt` is a string or array of lines; other fields default as today). Out: `ScenarioRegistry.All : IReadOnlyList<Scenario>` — one `Scenario` per valid def, in config order.

**How** — `HuddleConfig` parses `scenarios` as an array of `ScenarioDef` (the old `{ disabled, custom }` object is gone). `ScenarioRegistry` builds one `Scenario` per def, validating each (non-blank `key`/`systemPrompt`, unique `key`, recognized `effort`, and — Claude provider only — a Claude-aliasable `model`); invalid defs are skipped with a `Debug.WriteLine` warning. There is no built-in set to seed from, so an empty array yields no scenarios (vision still runs).

### 2. Run a due scenario

**Contract** — Unchanged. A `Scenario` (now always the config-driven type) whose `IsDue(now)` is true produces the same `ScenarioRequest { Model, MaxTokens, SystemPrompt, UserText, JsonSchema, Effort?, WebSearch }` and parses the same `NudgeDraft` → `Nudge` as before.

**How** — each scenario is a `ConfiguredScenario` (on the `Scenario` base for the throttle clock + concurrent-run gate), running `ExecuteAsync` from its `ScenarioDef`. `GetByKey` still serves the nudge card's display lookup.

## Goals / Non-Goals

**Goals:**
- Every scenario (prompt, cadence, model, effort, web-search) is defined in `huddle.config.json`; no code change to tune.
- A committed `huddle.config.example.json` reproduces today's four scenarios, as a copy-and-tune starting point.
- Simple mental model: the config array *is* the scenario set — what you see is what runs.

**Non-Goals:**
- Baked-in default scenarios or a fallback set (empty config = no scenarios).
- Per-key override/merge (the array is the literal, full set).
- Markdown scenario docs (the example config is the reference).

## Decisions

### D1: Configuration is the only source; nothing is baked in

The four prompts move out of code into the example config. An empty/absent `scenarios` yields no scenarios (moments still captured). This is the simplest model and matches the user's workflow (assemble a config per machine, often with an agent). The cost — no out-of-box scenarios — is acceptable for this audience and mitigated by the shipped example.

### D2: `scenarios` is a plain array, not `{ disabled, custom }`

With no built-ins there is nothing to disable and no built-in/custom distinction, so the wrapper and `disabled` are removed and `scenarios` becomes an array of definitions. Simpler to author and to reason about. (Machine-local configs are re-authored from the example, so the shape change costs nothing shipped.)

### D3: `ConfiguredScenario` stays as the single scenario type

The four built-in classes are deleted; `ConfiguredScenario` (built from a `ScenarioDef`) becomes the only scenario implementation, still on the `Scenario` base (throttle/gate). Collapsing the now-single-subclass base into `ConfiguredScenario` is a further cleanup, deliberately deferred to keep this change scoped to "move scenarios to config".

### D4: The example config lives at the repo root, committed

`huddle.config.example.json` is committed (the `.gitignore` rule matches only `huddle.config.json`, so the example is tracked). The user copies it to the resolved config location and renames it. It is the canonical, human-readable home for the default prompts.

## Risks / Trade-offs

- **[Parity]** → The example config must reproduce today's four scenarios (prompts verbatim as arrays of lines, same cadence/model/effort/trail). Verification bar: running on the example yields the same scenarios and nudge shapes as before.
- **[No out-of-box scenarios]** → an empty config produces no nudges; documented, and the example makes "like today" a one-file copy.
- **[Config format change]** → `scenarios` goes from object to array; local configs (incl. the value-delivery examples) must be re-authored. Machine-local and expected under this workflow.

## Migration Plan

No data migration. Ship the config-only registry + the example config. To keep today's behavior, copy `huddle.config.example.json` → `huddle.config.json`. Rollback: revert the change (built-in classes return).

## Open Questions

- None.
