## 1. Implementation

- [x] 1.1 Add `CopilotCliProvider.EffectiveModel(string scenarioModel)`: trim it; return the configured `_model` when it is blank or a bare Claude alias (`opus`/`sonnet`/`haiku`, case-insensitive), otherwise return it verbatim.
- [x] 1.2 `CompleteAsync` passes `EffectiveModel(request.Model)` to `--model` (vision `DescribeImageAsync` unchanged — no per-scenario model).
- [x] 1.3 README: document `model` as provider-relative — a Copilot-native per-scenario name is passed through; a bare Claude alias falls back to the top-level `model`.

## 2. Verify

- [x] 2.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 2.2 A Copilot scenario declaring a bare alias (`model: "sonnet"`) still runs (falls back to the top-level model); a Copilot-native name is passed through.
- [x] 2.3 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-21), Copilot provider (installed locally).

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Provider rejects Claude aliases (the basis)** — direct calls: `copilot --model opus` and `--model sonnet` both exit 1 (`Error: Model "opus"/"sonnet" from --model flag is not available`); `copilot --model claude-opus-5` exits 0. So aliases must fall back and Copilot-native names must pass through.

**Fallback, end-to-end** — a `huddle.config.json` with `provider: copilot`, built-ins disabled, and one custom scenario declaring `model: "sonnet"` (a bare alias): the scenario **emitted** a nudge (`scenario=alias-fallback`). Since Copilot rejects `sonnet`, the only way it ran is `EffectiveModel` substituting the configured `claude-opus-5` — confirming the fallback. Pass-through of a Copilot-native name is the same path all prior Copilot runs used (`claude-opus-5`, exit 0).
