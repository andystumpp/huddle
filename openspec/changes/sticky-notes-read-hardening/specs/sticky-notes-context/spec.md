## MODIFIED Requirements

### Requirement: Sticky Notes are an opt-in dynamic context source

The system SHALL read the user's Windows Sticky Notes only when the top-level config flag `stickyNotesContext` is enabled; the flag SHALL default to false. When enabled, it SHALL read the packaged Sticky Notes SQLite store at `%LOCALAPPDATA%\Packages\Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe\LocalState\plum.sqlite` with a read-only connection and collect the note text from the `Note` table, excluding soft-deleted notes (rows whose `DeletedAt` is not null) and ordering the results newest-first by `UpdatedAt`. Each note's stored text carries a leading internal `\id=<guid>` control token; the system SHALL strip that prefix so only the user's text is used, and SHALL drop any note that is empty after stripping. No length cap SHALL be applied — all matching notes are included. When the flag is off, the file is absent, or the read fails for any reason, the system SHALL produce no sticky-notes context and SHALL NOT disturb the scenario run.

#### Scenario: Disabled by default

- **WHEN** `stickyNotesContext` is absent or false
- **THEN** the Sticky Notes database is not read and no sticky-notes context is added to any scenario

#### Scenario: Enabled reads live notes read-only, newest-first

- **WHEN** `stickyNotesContext` is true and the Sticky Notes database exists
- **THEN** the system reads the non-deleted notes' text from the `Note` table over a read-only connection, ordered newest-first, without modifying the database

#### Scenario: Deleted notes are excluded

- **WHEN** a note has been deleted (its `DeletedAt` is set)
- **THEN** that note's text is not read and does not appear in any scenario's context

#### Scenario: The internal id prefix is stripped

- **WHEN** a note's stored text is `\id=<guid> <text>`
- **THEN** the injected context contains only `<text>`, with the `\id=<guid>` token removed

#### Scenario: Missing database is a graceful no-op

- **WHEN** `stickyNotesContext` is true but the database file does not exist (or the read throws)
- **THEN** no sticky-notes context is produced and the scenario runs exactly as it would with the flag off
