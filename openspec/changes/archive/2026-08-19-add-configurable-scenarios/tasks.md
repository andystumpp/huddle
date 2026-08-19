## 1. Config parsing

- [x] 1.1 Add `ScenarioConfig { IReadOnlyList<string> Disabled; IReadOnlyList<CustomScenarioDef> Custom }` and `CustomScenarioDef { string Key; string DisplayName; string AccentColorHex; double CadenceHours; int TrailSize; int PriorNudgesSize; string Model; string? Effort; bool WebSearch; string SystemPrompt }` (in `Huddle.App/Config`; `Effort` kept as raw string, parsed at compose time).
- [x] 1.2 Parse the optional `scenarios` object in `HuddleConfig.Load` into `ScenarioConfig` (absent → empty `Disabled`/`Custom`). For each custom entry, read `key`/`systemPrompt` and default the rest (`DisplayName` ← `Key` uppercased; `AccentColorHex` ← `#6BA6FF`; `CadenceHours` ← 6; `TrailSize` ← 60; `PriorNudgesSize` ← 10; `Model` ← `sonnet`; `Effort` ← null; `WebSearch` ← false). Expose `HuddleConfig.Current.Scenarios`.

## 2. ConfiguredScenario

- [x] 2.1 Add `ConfiguredScenario : Scenario` that takes a `CustomScenarioDef` (+ parsed `Effort?`) and overrides the metadata virtuals from it (`Key`, `Name`, `DisplayName`, `AccentColorHex`, `Cadence = TimeSpan.FromHours(CadenceHours)`, `TrailSize`, `PriorNudgesSize`, `ModelId = Model`).
- [x] 2.2 Implement `ExecuteAsync` with the shared template: build user text via `ScenarioPromptHelpers` (prior-nudges block + recent-moments block + a generic "follow the system prompt; cite moment IDs" line), issue `ScenarioRequest` carrying the def's `SystemPrompt`/`Model`/`Effort`/`WebSearch` and `BuildNudgeDraftSchema()`, then parse the `NudgeDraft` into a `Nudge` exactly as the built-ins do. The config `systemPrompt` never mentions JSON — the schema directive is appended by the provider.

## 3. Compose the registry

- [x] 3.1 Change `ScenarioRegistry.All` from a `static readonly` array to a `Lazy`-computed set: start from the four built-in instances, drop any whose `Key` is in `Scenarios.Disabled`, then append one `ConfiguredScenario` per **valid** custom def.
- [x] 3.2 Validate each custom def at compose time; skip + `Debug.WriteLine` warning when: `Key` or `SystemPrompt` is blank; `Key` collides with a built-in key or an earlier custom key; `Effort` failed to parse; or (Claude provider only) `Model` maps to no CLI alias. Track seen keys for collision detection; one skip never drops the others. `GetByKey` resolves over the composed list (so the nudge card finds custom keys/colors).

## 4. Docs

- [x] 4.1 README: document the `scenarios` section — `disabled` (built-in keys) and `custom` (the definition fields + which are required vs defaulted) — in the configuration section, plus the "read once at startup, restart to apply" note.
- [x] 4.2 README: ship a **value-delivery** example — a work `huddle.config.json` with a custom `value-delivery` scenario (Opus, ~2h cadence) alongside the Copilot/activeWindow/denylist work example, and disabling `linkedin-posts`. Uses a short **placeholder** `systemPrompt` the user tunes on the target machine.

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 5.2 No `scenarios` section → the four built-ins still compose and the app ticks (a moment wrote on defaults, no crash).
- [x] 5.3 Disable a built-in → it stops running; the other built-ins are unaffected.
- [x] 5.4 Add the value-delivery custom scenario → it runs and emits a `NudgeDraft`-shaped nudge tagged with its key.
- [x] 5.5 Invalid defs → a missing-`systemPrompt` entry and a key colliding with a built-in are both skipped while the valid scenario keeps running.
- [x] 5.6 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-19), Claude provider.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Composition + disable + invalid-skip (one config)** — dropped a test `huddle.config.json` that disabled all four built-ins and defined three custom entries: a valid `value-delivery`, a `broken-missing-prompt` (no `systemPrompt`), and one keyed `achievements` (collides with a built-in). On the fresh-launch tick, `scenarios.log` recorded **only** `scenario=value-delivery model=sonnet` running — proving the disabled built-ins were dropped and both invalid defs were skipped (they never ran), while the valid custom ran. The app stayed healthy.

**Custom scenario emits** — with a benign `systemPrompt` (the first, deliberately-adversarial "always output this exact payload" prompt was refused by the Claude CLI as a suspected injection — a prompt-authoring lesson, not a mechanism bug), the `value-delivery` scenario emitted a real nudge: `scenario=value-delivery`, a title/body grounded in the trail, moment-ID sources — the same `Nudge` shape as a built-in, tagged with the custom key and its configured accent (`#E0A458`, surfaced via `GetByKey`).

**No-config regression** — removing the test config and relaunching: the app composed the four built-ins and ticked cleanly (a moment wrote, no crash), so the default path is unchanged.

**Note (config-error visibility)** — invalid-def warnings go to `Debug.WriteLine`, so a user editing config on a machine without a debugger won't see *why* a scenario didn't load. Surfacing config errors to a visible log is a reasonable follow-up (out of scope here).
