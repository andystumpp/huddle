## Why

Authoring a scenario's `systemPrompt` as a single JSON string is unreadable for a long prompt — JSON strings can't hold literal newlines, so the whole prompt is one `\n`-escaped line. And the config parser is strict: it rejects `//` comments and trailing commas, so the annotated `jsonc` examples in the README don't actually parse, and a hand-editing mistake silently falls back to defaults. Make hand-authoring scenario config legible.

## What Changes

- `systemPrompt` accepts **either a string or an array of strings**. An array is joined with `\n` into one prompt (one element per line), so a long prompt can be written across multiple lines in the file. A short prompt stays a plain string. Both yield the same single prompt.
- The config parser tolerates comments and trailing commas (`JsonDocumentOptions { CommentHandling = Skip, AllowTrailingCommas = true }`), so an annotated `huddle.config.json` — as the README already shows — parses.
- README: document `systemPrompt` as string-or-array and the comment/trailing-comma tolerance; keep the examples accurate.

Non-goal (separate follow-up): surfacing config parse/validation errors visibly — a malformed file still falls back to defaults for now.

## Capabilities

### Modified Capabilities

- `scenario-config`: the inline custom-scenario definition — `systemPrompt` may be a string **or an array of lines joined with newlines**; and the configuration file MAY be annotated with comments and trailing commas.

## Impact

- **Code:** `HuddleConfig` — `ParseCustomScenario` reads `systemPrompt` as a string or a string array (joining an array with `\n`); `JsonDocument.Parse` gains `JsonDocumentOptions` (skip comments, allow trailing commas).
- **Docs:** README configuration section.
- **No** behavior change for existing string prompts; **no** schema/database change. Backward compatible.
