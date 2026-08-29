## 1. Config flag

- [x] 1.1 `HuddleConfig.cs`: add `public bool StickyNotesContext { get; init; }` (default false), parsed from the top-level `stickyNotesContext` (only explicit `true` enables).

## 2. Reader

- [x] 2.1 Add `src/Huddle.App/Memory/StickyNotesContext.cs`: `static string Read()`. Return `""` when the flag is off. Else read `%LOCALAPPDATA%\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite` with `Microsoft.Data.Sqlite`, connection `Mode=ReadOnly`, `SELECT text FROM note`; keep non-empty rows. Return a labelled, length-capped block (short header + notes) or `""` if none. Wrap everything in try/catch → `""` on any failure (missing file, lock, schema); never throw.

## 3. Inject

- [x] 3.1 `ConfiguredScenario.ExecuteAsync`: add `StickyNotesContext.Read()` to the `parts` list between the static `context` and the `profile` (omit when blank), so the prompt is `context + sticky-notes + profile + scenario prompt`, read fresh each run.

## 4. Example config

- [x] 4.1 `huddle.config.example.json`: document the top-level `stickyNotesContext` (commented, default off).

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 5.2 SQLite read recipe verified via a standalone harness.
- [x] 5.3 Graceful no-op: default flag off → app launches and runs the scenario path (which calls `StickyNotesContext.Read()`) without fault.
- [ ] 5.4 Real injection (manual, user, work laptop): with `stickyNotesContext: true`, a scenario's logged system prompt contains the note text, after the static context and before the profile.
- [x] 5.5 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-24.

**5.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**5.2 — SQLite read recipe (automated).** A file-based harness (`Microsoft.Data.Sqlite@10.0.9`) built a throwaway DB shaped like `plum.sqlite` (`note` table, `text` column) and read it back with the reader's exact recipe — `Mode=ReadOnly` + `SELECT text FROM note`:

```
  note: Ship the sticky-notes context feature
  note: Call dentist Thu
  (empty) -> skipped
  (null)  -> skipped
RESULT: PASS — read-only SELECT text FROM note returned the 2 non-empty notes
```

Confirms the table/column names, the query, read-only open, and null/empty filtering the reader depends on.

**5.3 — Graceful no-op.** The dev machine has no Sticky Notes DB and the flag defaults off. Launched the freshly built exe; a scenario tick ran (invoking `ConfiguredScenario.ExecuteAsync` → `StickyNotesContext.Read()`, which returns `""` at the flag guard) and the process stayed alive — the new injection path doesn't disturb the scenario run. The flag-on-but-absent path is guarded by `File.Exists` → `""` and wrapped in try/catch (by inspection).

**5.4 — Real injection (manual, user, work laptop).** With `"stickyNotesContext": true` in `%LOCALAPPDATA%\Huddle\huddle.config.json` and Sticky Notes present, a scenario's `--- system prompt ---` block in `scenarios.log` should contain the note text under "The user's current sticky notes…", positioned after any static `context` and before the `profile`. Left to the user — the dev machine has no Sticky Notes DB to read.
