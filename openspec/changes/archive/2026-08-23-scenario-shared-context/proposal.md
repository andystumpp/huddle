## Why

Shared facts about the user — role, situation, current goals — are today repeated inside individual scenario prompts (the LinkedIn prompt hard-says "The user is a principal-level software architect"). There is no single place to give Huddle context that every scenario draws on, so it has to be copied per scenario and drifts. A top-level `context` field lets you state who you are and what you're working on once, and have all scenarios use it.

## What Changes

- Add an optional top-level **`context`** field to `huddle.config.json` — free text, a string or an array of lines (same form as `systemPrompt`). Default empty.
- The context SHALL be **prepended to every scenario's `systemPrompt`** when the scenario call is built (context first, then the scenario's own instructions), so each scenario prompt no longer needs to restate who the user is. It is **always applied** — no per-scenario opt-out.
- Empty/absent `context` → scenarios run exactly as today.
- Scope is **scenarios only** — the vision (moment) prompt is unaffected.
- README documents the field; the example config shows it (as a placeholder).

Non-goals: applying context to vision/moments; per-scenario override or opt-out.

## Capabilities

### Modified Capabilities

- `scenario-config`: a top-level `context` from configuration is prepended to every configured scenario's system prompt.

## Impact

- **Code:** `HuddleConfig` gains a parsed `Context` (string or array-of-lines, default empty); the configured scenario prepends it to the request's `SystemPrompt` before the provider appends the schema directive.
- **Docs:** README configuration section; `huddle.config.example.json` gains a placeholder `context`.
- **No** provider change (context rides the existing system-prompt path), **no** DB/schema change, **no** change to vision. Backward compatible (empty context = today's behavior).
