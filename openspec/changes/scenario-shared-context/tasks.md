## 1. Config

- [x] 1.1 `HuddleConfig`: add `public string Context { get; init; } = ""`, parsed from the top-level `context` value with the same string-or-array-of-lines handling as `systemPrompt` (default empty). Factored the string-or-lines reader into a static `ReadStringOrLines(parent, name)` used by both `context` and a scenario's `systemPrompt`.

## 2. Prepend to scenarios

- [x] 2.1 In `ConfiguredScenario.ExecuteAsync`, the request's `SystemPrompt` is `Context + "\n\n" + def.SystemPrompt` when `HuddleConfig.Current.Context` is non-empty, otherwise just `def.SystemPrompt` (and the diagnostics log the composed prompt). Downstream (provider, schema directive) unchanged; vision untouched.

## 3. Docs

- [x] 3.1 README: documented the top-level `context` field (string or array of lines; prepended to every scenario; empty = no-op; scenarios only). Also fixed the stale `scenarios` row in the options table (it still described the removed built-in/`disabled` model).
- [x] 3.2 `huddle.config.example.json`: added a commented-out top-level `context` placeholder (so copying the example verbatim stays a no-op; the user deletes the `//` to enable).

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 4.2 With a `context` set, a scenario's logged system prompt begins with the context, then the scenario's own prompt.
- [x] 4.3 With no `context`, the scenario system prompt is the scenario's own prompt unchanged (by construction: empty → `_def.SystemPrompt`); vision never includes the context (by construction: `MomentExtractor` doesn't read `Context`).
- [x] 4.4 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-23), Claude provider.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Context is prepended** — dropped a config with `"context": ["TEST-CONTEXT-MARKER: …"]` and one benign scenario (`ctx-test`), launched, and `scenarios.log` showed the logged system prompt for that run starting with the marker, a blank line, then the scenario's own `systemPrompt`:
```
--- system prompt ---
TEST-CONTEXT-MARKER: the user is verifying Huddle's shared scenario context feature.

You are Huddle's Test Pulse. …
```

**No-op and vision** — empty/absent `context` yields the scenario's own prompt unchanged (the compose branch returns `_def.SystemPrompt`), and the vision path (`MomentExtractor`) never references `Context` — both by construction.

