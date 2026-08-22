## Why

The four scenarios are still compiled C# classes with hard-coded prompts, so tuning them on a work laptop means a code change. The user wants **all scenarios to be config**, so any prompt/cadence/model can be tuned per machine (often with an agent's help) without touching code. Recent work (config scenarios, array-of-lines prompts, per-scenario model) makes this the natural next step.

## What Changes

- **Scenarios come *only* from `huddle.config.json`.** The four built-in scenario classes (Achievements, Learnings, LinkedIn, Efficiency) are deleted; their prompts and settings move into a shipped example config. Every scenario runs through the single config-driven scenario implementation.
- **The `scenarios` config becomes a plain array** of scenario definitions — no `built-in` vs `custom` distinction, so no `disabled` and no per-key merge. What's in the array is exactly what runs. (Replaces the current `scenarios: { disabled, custom }` shape.)
- **A committed `huddle.config.example.json`** at the repo root spells out the provider plus all the default scenarios (prompts as arrays of lines) — a template the user copies to `huddle.config.json` (renaming) and tunes, by hand or with an agent. It reproduces today's scenarios exactly.
- **With no scenarios configured, no nudges are produced** — moments (vision) are still captured; there are no baked-in scenario defaults. Setup is "provide a config" (copy the example, or have an agent assemble one).
- **The scenario filter pills and nudge-card labels come from the configured scenarios**, not a fixed list — one pill per configured scenario (plus "All"). `displayName` is stored in natural case and uppercased only where the card shows it, so the config never has to be written in caps.

Non-goals: baked-in default scenarios / a fallback set; per-key override/merge (the config is the full, literal set); markdown scenario docs (the example config is self-documenting).

## Capabilities

### Modified Capabilities

- `scenario-config`: the active scenario set comes entirely from a `scenarios` **array** in configuration (no built-ins, no disable/merge); an empty/absent set produces no scenarios; a committed example config carries the defaults.

## Impact

- **Code:** delete `AchievementsScenario`, `LearningsScenario`, `LinkedInPostsScenario`, `EfficiencyInsightsScenario`; `CustomScenarioDef` → `ScenarioDef` and `scenarios` becomes a `ScenarioDef[]` (drop the `{ disabled, custom }` wrapper); `HuddleConfig` parses `scenarios` as an array; `ScenarioRegistry` composes only from config (validate + skip bad defs). `ConfiguredScenario` stays as the single scenario type (on the `Scenario` base). Scenario prompt text moves out of code into the example config.
- **UI:** the filter pills build from `ScenarioRegistry` at panel startup (`BuildFilterChips`) instead of a hardcoded XAML list; `NudgeCard` uppercases the `displayName` at the tag; the `displayName` default becomes the key (natural case).
- **Repo:** add `huddle.config.example.json` (committed); README scenarios section rewritten (config-only, point to the example, note no-config = no nudges).
- **Behavior:** an empty config yields no scenarios (a change from today's four built-ins); the example config reproduces today's behavior. No DB/provider change.
