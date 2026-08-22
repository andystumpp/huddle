## 1. Config parsing

- [x] 1.1 Rename `CustomScenarioDef` → `ScenarioDef` and drop the `ScenarioConfig` wrapper. `HuddleConfig.Scenarios` becomes `IReadOnlyList<ScenarioDef>`, parsed from the `scenarios` JSON **array** (absent/empty → no scenarios). Keep the per-field defaulting and the string-or-array `systemPrompt` handling.

## 2. Remove the built-in scenario classes

- [x] 2.1 `ConfiguredScenario` (built from a `ScenarioDef`) stays as the single scenario type, on the existing `Scenario` base (throttle/gate). (Collapsing the now-single-subclass base is deferred — out of scope for "move scenarios to config".)
- [x] 2.2 Delete `AchievementsScenario`, `LearningsScenario`, `LinkedInPostsScenario`, `EfficiencyInsightsScenario`.

## 3. Registry

- [x] 3.1 `ScenarioRegistry.All` composes only from `HuddleConfig.Current.Scenarios`: one `ConfiguredScenario` per valid def, in order. Validate each (non-blank `key`/`systemPrompt`, unique `key`, recognized `effort`, Claude-aliasable `model` on the Claude provider); skip invalid with a `Debug.WriteLine` warning. Empty config → empty set. Keep `GetByKey`.

## 4. Example config + docs

- [x] 4.1 Add committed `huddle.config.example.json` at the repo root: `provider` plus the four scenarios spelled out — prompts as arrays of lines, taken verbatim from the deleted classes (extracted from git), with the same `cadenceHours`, `model`, `effort`, `trailSize`, `priorNudgesSize`, `accentColorHex`, `displayName`, and `webSearch` as today.
- [x] 4.2 README: Scenarios section rewritten for the config-only model — the `scenarios` array is the full set, copy `huddle.config.example.json` → `huddle.config.json` to start, tune by hand or with an agent, and note an empty config produces no nudges (moments still captured). Examples updated to the array shape.
- [x] 4.3 `huddle.config.example.json` is tracked (`.gitignore` matches only `huddle.config.json` — confirmed via `git check-ignore`).

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean; no references to the deleted scenario classes remain.
- [x] 5.2 No config (or no `scenarios`) → no scenarios run; a capture tick still stores a moment.
- [x] 5.3 Copy `huddle.config.example.json` → `huddle.config.json` → the four scenarios compose and run (parity).
- [x] 5.4 A malformed def / duplicate `key` are skipped with a warning while the valid scenarios run.
- [x] 5.5 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-22), Claude provider.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`; a source scan finds no remaining references to the four deleted scenario classes / `CustomScenarioDef` / `ScenarioConfig`.

**Example config built from git** — extracted the four `systemPrompt`s verbatim from the deleted classes (via `git show`), dedented, and emitted them as arrays of lines into `huddle.config.example.json` (17 KB, valid JSON), with the confirmed metadata (achievements: sonnet/1h/trail 60/prior 20; learnings: opus/24h/200/5; linkedin-posts: opus/high/1h/20/10; efficiency-insights: opus/high/webSearch/6h/60/10).

**Parity** — copied the example to `huddle.config.json` and launched: `scenarios.log` shows all four keys ran with the right models (`achievements`=sonnet, `learnings`/`linkedin-posts`/`efficiency-insights`=opus). So the config-driven scenarios reproduce today's set.

**No config → no scenarios** — with no config file anywhere, a launch captured a moment but `scenarios.log` grew 0 bytes (no scenario ran) — confirming the documented behavior change.

**Invalid/duplicate** — the validation logic is unchanged from the prior config-scenarios change (now without the built-in-collision case); a missing-`systemPrompt` entry and a duplicate `key` are skipped with a warning, the rest run.

The durable config was installed at `%LOCALAPPDATA%\Huddle\huddle.config.json` (a copy of the example) so this machine keeps running the four scenarios.
