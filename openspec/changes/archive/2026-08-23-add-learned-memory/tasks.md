## 1. Profile store

- [x] 1.1 Add `ProfileStore` (Huddle.App): reads/writes `%LOCALAPPDATA%\Huddle\profile.md`. `Read()` → the file text or `""` if missing; `Write(string)`; `IsDue(TimeSpan)` → missing or older than the interval (the file's mtime is the "last run" clock, so no separate accessor is needed).

## 2. Reflection job

- [x] 2.1 Add the reflection job (Huddle.App): when `ProfileStore.IsDue(24h)`, read the current profile + recent moments (`MomentStore.RecentAsync`), build the rollup prompt (drift guard: preserve existing facts unless superseded; promote only stable/repeated signals; sections Projects / People & context / Decisions & adopted / Preferences & working style / Current focus; ~1 page; never sensitive values), and call `ICliProvider.CompleteAsync` with model `opus`, `Effort.XHigh`, and a `{ "profile": string }` schema. Parse the `profile` field and `ProfileStore.Write` it. Not a scenario; emits no nudge.
- [x] 2.2 Invoke the reflection from `OnSchedulerTick` (sibling to `RunScenariosAsync`), guarded by `IsDue`.

## 3. Inject into scenarios

- [x] 3.1 `ConfiguredScenario.ExecuteAsync`: compose the request `SystemPrompt` as `context + profile + def.SystemPrompt` (each part omitted when blank), reading the profile fresh from `ProfileStore` each run.

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 4.2 Reflection: with a trail present and no/old `profile.md`, a tick writes a `profile.md` (markdown, sectioned) at `opus`/`xhigh`; a fresh `profile.md` (<24h) skips the reflection.
- [x] 4.3 Injection: with a non-empty `profile.md`, a scenario's logged system prompt contains `context`, then the profile, then the scenario's own prompt; an empty/absent profile is a no-op.
- [x] 4.4 Record commands and outcomes in §Verification.

## Verification

All checks run on the real machine (`%LOCALAPPDATA%\Huddle`), provider `claude`, 2026-08-23.

**4.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`. (The 6 `NU1903` warnings are the pre-existing `SQLitePCLRaw.lib.e_sqlite3` advisory, unrelated to this change.)

**4.2 — Reflection writes the profile at opus/xhigh.** Started from no `profile.md` and a live `huddle.db` trail. Launched the app; the immediate tick's reflection wrote `%LOCALAPPDATA%\Huddle\profile.md` (2883 bytes) with the exact prescribed sections — `## Projects`, `## People & context`, `## Decisions & adopted`, `## Preferences & working style`, `## Current focus` — ~1 page, no sensitive values. `ReflectionJob` requested `Model: "opus"`, `Effort.XHigh`; the CLI returned parseable `{"profile": …}` and it was written.

**4.2 — Staleness gate.** Restarted the app with the now-fresh `profile.md` (<24h old). The tick ran but `profile.md`'s mtime stayed `19:27:27` — `ProfileStore.IsDue(24h)` returned false, so the reflection was skipped. No re-write.

**4.3 — Injection order.** After the restart, `_lastRun` resets to `MinValue`, so the first tick ran all scenarios with the profile present. `scenarios.log` (written verbatim by `ScenarioDiagnostics.LogRun`) shows the `efficiency-insights` run's `--- system prompt ---` block opening with the full profile (`You are Andy (\`andystumpp\`…`) immediately followed by the scenario's own prompt (`You are Huddle's Efficiency Insights scenario.`). The real config has no top-level `context`, so that blank part is omitted — matching the "each part omitted when blank" rule (`context → profile → scenario prompt`). The empty-profile no-op is the state before the first reflection, when scenario prompts logged without any profile prefix.
