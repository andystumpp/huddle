## 1. Config field

- [x] 1.1 `ScenarioConfig.cs`: add `public string? Agent { get; init; }` (default null).
- [x] 1.2 `HuddleConfig.ParseScenario`: set `Agent` from the scenario's `agent` string (null when absent), same pattern as `effort`.

## 2. Carry it on the request

- [x] 2.1 `ICliProvider.cs`: add an optional `string? Agent = null` to the `ScenarioRequest` record (after `Effort`/`WebSearch`).
- [x] 2.2 `ConfiguredScenario.ExecuteAsync`: pass `Agent: _def.Agent` when building the request.

## 3. Copilot passes --agent

- [x] 3.1 `CopilotCliProvider.CompleteAsync`: when `request.Agent` is non-empty, add `--agent` + the name to the argument list (next to `--model`), additive. Claude provider unchanged (ignores it).

## 4. Example config

- [x] 4.1 `huddle.config.example.json`: document the per-scenario `agent` field (commented).

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 5.2 Config parse — the `agent` string maps to `ScenarioDef.Agent` (null when absent), mirroring the `effort` parse.
- [x] 5.3 Arg assembly — `CopilotCliProvider` adds `--agent <name>` only when `request.Agent` is non-empty, alongside `--model`; Claude never reads it.
- [x] 5.4 App launches and runs the scenario path without fault (default provider claude, no agent).
- [ ] 5.5 Real Copilot run against a user-built agent — manual, work machine (Copilot auth + agents live there).
- [x] 5.6 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-29.

**5.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**5.2 / 5.3 — Plumbing (by inspection).** The `agent` parse mirrors the verified `effort` pattern (`TryGetProperty("agent") … ? GetString() : null`) into `ScenarioDef.Agent`; `ConfiguredScenario` passes `Agent: _def.Agent` onto the `ScenarioRequest`; `CopilotCliProvider` adds `--agent` + the name only when `request.Agent` is non-empty, additive with `--model`. The flag itself was confirmed real earlier (`copilot --help` → `--agent <agent>  Specify a custom agent to use`). The Claude provider never reads `request.Agent`, so it is a no-op there.

**5.4 — No-regression smoke.** Launched the freshly built exe (default `claude` provider, no agent configured); the scenario path ran with the new `ScenarioRequest.Agent` field (null) without fault.

**5.5 — Real agent run (manual, work machine).** The dev machine uses the `claude` provider and has no custom Copilot agents, so the actual `--agent` invocation can only be exercised where Copilot auth + the user's agents live: set `provider: copilot` (or agency) and a scenario `"agent": "<name>"`, and confirm the scenario runs through that agent and still returns a `NudgeDraft`.
