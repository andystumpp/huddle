## Context

The `Scenario` abstraction landed in the last change with per-scenario throttle, trail sizing, prior-nudges sizing, and visual identity. Model selection is the same shape — a per-scenario knob that should live on the class, not inside `ExecuteAsync` as a hardcode. Andy wants LinkedIn on Opus (it's a quality-sensitive single emit; one bad post burns trust) while Achievements stays on Sonnet (long trail, short body, Sonnet is fast and cheap).

## Goals / Non-Goals

**Goals:**

- `Scenario` declares a `ModelId` property with a sensible default (Sonnet).
- LinkedIn overrides to Opus 4.8 (latest Opus, satisfies the "at least 4.7" floor and matches the `claude-api` skill's recommended default).
- Achievements stays on Sonnet without an override.
- `scenarios.log` records the actual model used per run, not a hardcoded label.

**Non-Goals:**

- No per-scenario `max_tokens` override. Different scenarios may eventually want this; for now the existing 600 covers both.
- No runtime model override (env var, file, UI). Class-level only for this change.
- No effort / thinking parameter overrides. Opus 4.8 adaptive thinking is the default per the `claude-api` skill; we don't tune it here.

## Decisions

### D1. Property on the base class, default to Sonnet

`Scenario` gains `public virtual Model ModelId => Model.ClaudeSonnet4_6;`. Subclasses can override when they need something else. This matches the shape of `PriorNudgesSize` (also a virtual with a default).

### D2. LinkedIn picks Opus 4.8

Floor was "Opus 4.7 at least"; Opus 4.8 is the current recommended Opus per the `claude-api` skill ("ALWAYS use claude-opus-4-8 unless the user explicitly names a different model"). Cost-per-call roughly doubles vs Sonnet but stays under a penny.

### D3. Achievements keeps the default

No override, inherits Sonnet 4.6. The Achievements scenario benefits from speed (long 60-moment input + frequent silent emits) and Sonnet is the right tier.

### D4. `ScenarioDiagnostics.LogRun` takes a model label

Currently the log header reads `model=claude-sonnet-4-6` hardcoded. After this change it reflects the actual model. Each scenario passes `ModelId.Value` (or `.ToString()` if Value isn't available on the typed wrapper) when logging.

## Risks / Trade-offs

- **[Cost shift for LinkedIn]** → ~$0.05/day at the hourly cadence. Flagged in the proposal; bounded by the existing cadence + pause-on-lock.
- **[`Model.Value` API surface]** → The SDK wraps model IDs as a typed enum-style; depending on the Anthropic SDK version, the access might be `.Value`, `.ToString()`, or a static helper. We pick whatever compiles cleanly; both yield the bare model-id string.

## Open Questions

- Should the scenario-diagnostic header also include `effort` / `thinking` once those become per-scenario? Not in this change.
- Should we expose `ModelId` via the registry so the UI can show a tiny model label per nudge ("via opus-4-8")? Out of scope; nudge cards stay clean.
