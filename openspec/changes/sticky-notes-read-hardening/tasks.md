## 1. Harden the reader

- [x] 1.1 `StickyNotesContext.cs`: change the query to `SELECT Text FROM Note WHERE DeletedAt IS NULL ORDER BY UpdatedAt DESC` (live notes only, newest-first).
- [x] 1.2 Strip the leading `\id=<guid>` token from each row's text before normalizing (`Regex ^\\id=\S+\s*`); drop a note that is empty after stripping.
- [x] 1.3 Remove the `MaxChars` cap and the truncation branch — inject all matching notes.

## 2. Verify

- [x] 2.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 2.2 Against the real Sticky Notes DB, the hardened recipe yields clean stripped text, deleted excluded, newest-first, no cap — confirmed via a harness.
- [x] 2.3 App launches and runs the scenario path without fault (flag default off).
- [x] 2.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-29.

**2.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**2.2 — Real-DB read (automated).** A harness mirroring the hardened `ReadNotes` (`SELECT Text FROM Note WHERE DeletedAt IS NULL ORDER BY UpdatedAt DESC`, `\id=<guid>` strip, no cap) ran against the actual Sticky Notes DB:

```
live notes: 1
---- exact block the hardened reader injects ----
The user's current sticky notes (their live work plan / reminders):
- Hello this is something
---- checks ----
PASS: no \id= prefix leaked
```

The stray `\id=<guid>` prefix (present in the raw `Text`) is gone; the injected block is just the user's words.

**2.3 — No-regression smoke.** Launched the freshly built exe (flag default off); the scenario path ran without fault.

Note: the underlying schema findings driving this change — `Note.Text` = `\id=<guid> <text>`, `Note.DeletedAt` NULL for live notes, `Note.UpdatedAt` a tick count — were read directly from the live `plum.sqlite`.
