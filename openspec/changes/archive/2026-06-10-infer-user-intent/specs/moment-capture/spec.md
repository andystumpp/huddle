## MODIFIED Requirements

### Requirement: Claude vision call

The system SHALL send the captured JPEG, the foreground context, and a list of the most recent prior moments to the Claude API via the official Anthropic C# SDK, using the model `claude-sonnet-4-6` and `max_tokens` of `250`. The system prompt SHALL instruct Claude to infer what the user is currently trying to accomplish — framing the response as **intent** ("you're trying to X", "you're verifying X") rather than **description** ("you're looking at X") — in a second-person, dry, specific, 1–2 sentence reply, hedging when the trail doesn't pin the goal down and committing when the trajectory is unambiguous. The user message SHALL contain the image, then a "Recent moments" text block listing up to 6 prior moments newest-first (relative time, app, abbreviated window title, prior summary), then a `Foreground app: {app}\nWindow title: {title}` block describing the current frame. When no prior moments exist, the "Recent moments" block SHALL be omitted entirely.

#### Scenario: Successful call returns an intent-framed summary

- **WHEN** the API call completes successfully with sufficient trail
- **THEN** the response's first text block is treated as the moment summary and reads as an inferred goal ("you're trying to / you're verifying / you're likely working on …") rather than a screen description

#### Scenario: First-ever capture has no trail

- **WHEN** the call fires with an empty moment store
- **THEN** the user message contains only the image and the foreground block; no "Recent moments" section is rendered, and the model still produces a single-shot intent-framed summary

#### Scenario: Trail is included for subsequent captures

- **WHEN** the call fires with N existing moments in the store (1 ≤ N ≤ 6)
- **THEN** the user message contains a "Recent moments" block with those N moments listed newest-first, ahead of the current foreground block

#### Scenario: Trail is capped at 6 moments

- **WHEN** the moment store contains more than 6 moments
- **THEN** only the 6 most recent are included in the "Recent moments" block

#### Scenario: Model is Sonnet 4.6

- **WHEN** the API call is sent
- **THEN** the model field is `claude-sonnet-4-6` and the request's `max_tokens` is `250`
