## Why

Reading a real Sticky Notes database (current Windows build) surfaced three problems the shipped reader has, which only show up against actual data:

- **Deleted notes leak in.** The `Note` table soft-deletes — a deleted note stays as a row with `DeletedAt` set (non-deleted rows have `DeletedAt = NULL`). The current query (`SELECT text FROM note`) doesn't filter it, so notes the user deleted would be injected as context.
- **A control prefix leaks in.** Each note's `Text` value is stored as `\id=<guid> <the note text>` — a leading internal id token then the text. So the reader injects a stray GUID (`\id=7d30…`) ahead of every note.
- **A char cap the user doesn't want.** The reader truncates at 4000 chars; the user wants all notes injected.

Arbitrary row order is a minor fourth issue — newest-first is more useful and stable.

## What Changes

- Query non-deleted notes, newest first: `SELECT Text FROM Note WHERE DeletedAt IS NULL ORDER BY UpdatedAt DESC`.
- Strip the leading `\id=<guid>` control token from each note before injecting, leaving just the user's text (a note that is empty after stripping is dropped).
- Remove the 4000-char cap — inject all notes.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `sticky-notes-context`: the read now excludes deleted notes, orders newest-first, strips the internal `\id=<guid>` prefix, and applies no length cap.

## Impact

- `src/Huddle.App/Memory/StickyNotesContext.cs` — updated query (deleted filter + order), a prefix strip, and removal of the `MaxChars` cap.
- No config or spec-composition change; the injection seam and opt-in flag are unchanged.
