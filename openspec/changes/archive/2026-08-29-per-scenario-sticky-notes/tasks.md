## 1. Per-scenario field

- [x] 1.1 `ScenarioConfig.cs`: add `public bool StickyNotes { get; init; }` (default false).
- [x] 1.2 `HuddleConfig.ParseScenario`: set `StickyNotes = Bool("stickyNotes", false)`.
- [x] 1.3 `HuddleConfig.cs`: remove the top-level `StickyNotesContext` property and its parse in `Load`.

## 2. Move the gate

- [x] 2.1 `StickyNotesContext.Read()`: remove the `if (!HuddleConfig.Current.StickyNotesContext) return ""` guard — it now just reads.
- [x] 2.2 `ConfiguredScenario.ExecuteAsync`: call `StickyNotesContext.Read()` and add the block only when `_def.StickyNotes` is true.

## 3. Example config

- [x] 3.1 `huddle.config.example.json`: remove the top-level `stickyNotesContext` comment; document a per-scenario `stickyNotes` example.

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 4.2 Per-scenario gate — the injection is guarded by `if (_def.StickyNotes)`; an opted-out scenario neither reads nor injects. See note.
- [x] 4.3 App launches and runs the scenario path without fault (no scenario opts in by default).
- [x] 4.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-29.

**4.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**4.2 — Per-scenario gate.** The change moves the opt-in only: `ScenarioDef.StickyNotes` parses via `Bool("stickyNotes", false)` (same pattern as `showInAll`/`webSearch`), and `ConfiguredScenario` injects the notes only inside `if (_def.StickyNotes)`. `StickyNotesContext.Read()` (read-only, deleted-filtered, `\id=`-stripped, newest-first, no cap) is unchanged apart from dropping the removed global-flag guard, and its output was already verified clean against the real `plum.sqlite` in the read-hardening change. An end-to-end check via `scenarios.log` (a temp config with a `stickyNotes: true` scenario) was inconclusive here — the sandbox writes the launched app's log to an overlay the verifying read couldn't see — so the injection itself is confirmed by construction (proven read + literal gate) plus the manual step below.

**4.3 — No-regression smoke.** Launched the freshly built exe with the default config (no scenario opts in); the scenario path ran with the moved gate and the removed top-level flag without fault.

**Manual (user):** set `"stickyNotes": true` on a scenario in `%LOCALAPPDATA%\Huddle\huddle.config.json`; that scenario's logged system prompt gains the "The user's current sticky notes…" block, and scenarios without the flag do not.
