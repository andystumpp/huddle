## MODIFIED Requirements

### Requirement: The completion request always carries a JSON output schema

Every scenario completion request SHALL include a JSON output schema describing the `NudgeDraft` object; the field is required, not optional. Because the CLI has no structured-output parameter, each provider SHALL append instructions to its prompt directing the model to respond with a single JSON object conforming to that schema and nothing else. The Claude provider appends the directive to its system prompt; the Copilot provider folds it into the combined prompt and then isolates the first balanced JSON object from stdout, because Copilot prefaces the object with conversational prose. In every case the scenario SHALL parse the returned text into its `NudgeDraft` using the same deserialization.

The schema SHALL also constrain the *format* of the emitted text through field descriptions: the `title` field SHALL describe a short, plain-language concept — an at-a-glance label naming the idea or outcome, bounded to roughly eight words — and the `body` field SHALL carry the specifics, evidence, named sources, and any URLs. Because every scenario shares this one schema and both providers serialize it into the prompt, the format constraint reaches every scenario uniformly, while each scenario's own prompt still owns what its title is about.

#### Scenario: A provider requests the schema in the prompt

- **WHEN** a scenario runs on the Claude provider
- **THEN** the system prompt sent to `claude` instructs it to emit only a JSON object matching the `NudgeDraft` schema, and the returned text deserializes into a `NudgeDraft` with the expected fields

#### Scenario: Copilot output is isolated to the first JSON object

- **WHEN** a scenario runs on the Copilot provider and stdout wraps the JSON object in conversational prose
- **THEN** the provider isolates the first balanced JSON object from stdout and that text deserializes into a `NudgeDraft`

#### Scenario: The schema bounds the title to a concept

- **WHEN** a scenario completion request is built
- **THEN** the output schema's `title` field carries a description bounding it to a short, plain-language concept (roughly eight words, naming the idea rather than restating the whole insight), and the `body` field's description directs the specifics, sources, and URLs there
