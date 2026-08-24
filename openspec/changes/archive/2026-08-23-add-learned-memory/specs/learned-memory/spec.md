## ADDED Requirements

### Requirement: A daily reflection distils the trail into a profile

The system SHALL run a **reflection job**, separate from scenarios, that maintains a durable profile of the user. The job SHALL be due when the profile file is missing or older than 24 hours (its cadence tracked by the file's modified time). When due, it SHALL read the current profile and the recent moment trail, make one completion through the configured provider at model `opus` with extra-high (`xhigh`) effort, and overwrite the profile with the returned markdown. The job SHALL produce no nudge and SHALL not run through the scenario pipeline.

#### Scenario: The reflection runs when the profile is stale

- **WHEN** a capture tick fires and the profile file is missing or older than 24 hours
- **THEN** the reflection job reads the current profile and recent moments, calls the provider at `opus`/`xhigh`, and overwrites the profile; no nudge is produced

#### Scenario: The reflection is skipped when fresh

- **WHEN** a capture tick fires and the profile file was written less than 24 hours ago
- **THEN** the reflection job does not run

### Requirement: The profile is a durable, user-editable file

The profile SHALL be stored as a single markdown file at `%LOCALAPPDATA%\Huddle\profile.md`, next to the configuration and database, so the user can open, read, and edit it directly. It SHALL NOT be stored in the database. The reflection SHALL rewrite the whole document, preserving existing facts unless the recent trail clearly supersedes them and promoting only stable, repeated signals to durable facts. The profile SHALL NOT contain specific sensitive values (salaries, account numbers, passwords, medical values, personal identifiers), inheriting the same rule as moment summaries.

#### Scenario: The profile is a readable file the user can edit

- **WHEN** the reflection has run at least once
- **THEN** `%LOCALAPPDATA%\Huddle\profile.md` exists as markdown the user can open and edit, and a subsequent scenario run uses the edited content

#### Scenario: Sensitive values are excluded from the profile

- **WHEN** the reflection distils moments that concerned sensitive content
- **THEN** the profile describes the kind of work without any specific sensitive values

### Requirement: The profile is prepended to every scenario

Each scenario's system prompt SHALL be composed as the static `context`, then the learned profile, then the scenario's own `systemPrompt` (each part omitted when empty). The profile SHALL be read fresh at each scenario run, so a new reflection or a manual edit takes effect without restarting the app.

#### Scenario: A scenario prompt includes the profile

- **WHEN** a scenario runs and `profile.md` is non-empty
- **THEN** its system prompt is `context + profile + the scenario's own prompt`, in that order

#### Scenario: An empty profile is a no-op

- **WHEN** no profile exists yet (before the first reflection)
- **THEN** scenarios run with just `context + their own prompt`, exactly as before this change
