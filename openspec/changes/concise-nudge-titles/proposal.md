## Why

Nudge titles read like implementation details, not concepts. In practice the model treats the title as the whole payload — a run-on sentence stuffed with identifiers (type names, PR numbers, CLI commands, filenames) — leaving the body redundant. Real examples:

- *"Your per-key config merge is verified by relaunching Huddle — lock the resolved ConfiguredScenario output with Verify snapshot tests so a merge-strategy regression fails a test, not a runtime eyeball"*
- *"You re-read the OPSX workflow docs 11 times over 7 hours before proposing — that circling-to-orient is exactly what OpenSpec's `/opsx:explore` step is for"*

Each scenario prompt only says the title is "in one line," with no length ceiling and no "name the concept, not the sentence," so "one line" becomes one long jargon-dense sentence. A title should be a short concept the user can skim at a glance; the specifics belong in the body.

## What Changes

- The shared `NudgeDraft` output schema constrains the `title` field to a short, plain-language concept (a length ceiling, no full sentences, no code identifiers), and constrains `body` to carry the specifics, evidence, sources, and any URLs.
- Because every scenario completion request runs through the one shared schema (`BuildNudgeDraftSchema`) and each CLI provider serializes that schema into its prompt, this applies to all four scenarios uniformly with a single edit. The per-scenario prompts keep owning *what* the title is about (achievement / claim / hook / improvement).
- The learned-memory reflection is untouched — it uses its own profile schema and has no title.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `scenario-backend`: the required nudge output schema now also constrains the *format* of `title` and `body`, not just their presence.

## Impact

- `src/Huddle.App/Scenarios/ScenarioPromptHelpers.cs` — `BuildNudgeDraftSchema()` gains `description` on the `title` and `body` properties.
- No new fields, no config changes, no rebuild of the scenario/config surface. Purely output-quality tuning to the shared schema; the change reaches the model because both CLI providers already inline the schema JSON via `BuildSchemaDirective`.
