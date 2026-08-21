## MODIFIED Requirements

### Requirement: Claude vision call

The system SHALL produce each moment summary by sending the captured screenshot and the foreground context to the **selected local CLI provider** (Claude, Copilot, or Agency), not to any API/SDK. The captured JPEG SHALL be written to a temporary file and attached to a single non-interactive CLI prompt — for the Claude provider via an `@<path>` reference in the prompt, for the Copilot/Agency provider via `--attachment <path>`. The prompt SHALL instruct the model to infer what the user is currently trying to accomplish — framing the response as **intent** ("you're trying to X", "you're verifying X") rather than **description** ("you're looking at X") — in a second-person, dry, specific, 1–2 sentence reply, hedging when the trail doesn't pin the goal down and committing when the trajectory is unambiguous. The prompt SHALL include, when present, a "Recent moments" block listing up to 6 prior moments newest-first (relative time, app, abbreviated window title, prior summary), and a `Foreground app: {app}\nWindow title: {title}` block for the current frame. When no prior moments exist, the "Recent moments" block SHALL be omitted.

The prompt SHALL additionally instruct the model to (a) **never include specific sensitive values** in the summary — no salaries, dollar amounts, account or card numbers, passwords, medical values, or personal identifiers — describing only the *kind* of thing, and (b) judge whether the frame shows sensitive personal, financial, health, credential, or PII content. The model SHALL reply with a single JSON object `{"summary": …, "sensitive": true|false}`; the system SHALL isolate that object from stdout and take the parsed `summary` as the moment summary and `sensitive` as the sensitivity flag. When the reply is not parseable JSON, the system SHALL take the whole reply as the summary and treat the frame as not sensitive.

#### Scenario: Successful call returns an intent-framed summary

- **WHEN** the CLI call completes successfully with sufficient trail
- **THEN** the parsed `summary` is treated as the moment summary and reads as an inferred goal ("you're trying to / you're verifying / you're likely working on …") rather than a screen description

#### Scenario: The screenshot is attached to the CLI

- **WHEN** the vision call runs on the Claude provider
- **THEN** the screenshot path is referenced with `@<path>` in the prompt; on the Copilot/Agency provider it is passed with `--attachment <path>`

#### Scenario: Summaries omit sensitive values

- **WHEN** the frame shows sensitive values (for example a compensation statement with salary and bonus figures)
- **THEN** the returned summary describes the kind of content ("reviewing a confidential compensation statement") and contains none of the specific values

#### Scenario: The frame is judged for sensitivity

- **WHEN** the frame shows sensitive personal, financial, health, credential, or PII content
- **THEN** the reply's `sensitive` flag is true; for ordinary non-sensitive content it is false

#### Scenario: A non-JSON reply is still used as the summary

- **WHEN** the model replies with plain text rather than the JSON object
- **THEN** the whole reply is taken as the summary and the frame is treated as not sensitive (the moment is not lost)

#### Scenario: First-ever capture has no trail

- **WHEN** the call fires with an empty moment store
- **THEN** the prompt contains only the image and the foreground block; no "Recent moments" section is rendered, and the model still produces a single-shot intent-framed summary

#### Scenario: Trail is included for subsequent captures

- **WHEN** the call fires with N existing moments in the store (1 ≤ N ≤ 6)
- **THEN** the prompt contains a "Recent moments" block with those N moments listed newest-first, ahead of the current foreground block

#### Scenario: Trail is capped at 6 moments

- **WHEN** the moment store contains more than 6 moments
- **THEN** only the 6 most recent are included in the "Recent moments" block

## ADDED Requirements

### Requirement: Sensitive frames are skipped by default

The configuration SHALL support a `skipSensitiveMoments` toggle that defaults to **enabled**. When a captured frame is flagged sensitive by the vision call and the toggle is enabled, the system SHALL store no moment for that tick. When the toggle is disabled, the system SHALL store the moment using the (already value-free) summary. Summaries are value-free regardless of this toggle; the toggle only decides whether a flagged frame is dropped entirely.

#### Scenario: A sensitive frame is skipped by default

- **WHEN** a capture tick's frame is flagged sensitive and `skipSensitiveMoments` is at its default
- **THEN** no moment is stored for that tick

#### Scenario: Keeping sensitive moments is opt-out

- **WHEN** `skipSensitiveMoments` is set to `false` and a frame is flagged sensitive
- **THEN** a moment is stored using the value-free summary

#### Scenario: Non-sensitive frames store normally

- **WHEN** a frame is not flagged sensitive
- **THEN** a moment is stored as normal, regardless of the toggle
