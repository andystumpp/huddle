## 1. Parser

- [x] 1.1 In `HuddleConfig.Load`, pass `new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }` to `JsonDocument.Parse` so `huddle.config.json` may contain `//` / `/* */` comments and trailing commas.
- [x] 1.2 In `ParseCustomScenario`, read `systemPrompt` as string **or** array: if the element is a `String`, use it; if it is an `Array`, join its `String` elements with `\n`; otherwise `""`. Downstream (`CustomScenarioDef.SystemPrompt`, `ConfiguredScenario`) is unchanged.

## 2. Docs

- [x] 2.1 README: document that `systemPrompt` accepts a string or an array of lines (joined with `\n` into one prompt — one element per line, still one prompt), and that `huddle.config.json` may contain comments and trailing commas. Show a custom scenario whose `systemPrompt` is an array of lines.

## 3. Verify

- [x] 3.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 3.2 A config with a `//`-commented, trailing-comma `huddle.config.json` parses (does not silently fall back), and a custom scenario with an **array** `systemPrompt` composes and runs — its joined prompt is used (a nudge emits or it runs), identical to the same prompt as a single string.
- [x] 3.3 Backward compatible: an existing string `systemPrompt` and a comment-free config parse exactly as before.
- [x] 3.4 Record commands and outcomes in §Verification.

## Verification

Verified on the personal machine (2026-08-19), Claude provider.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**Comments + trailing commas + array prompt (one config)** — dropped a `huddle.config.json` containing a `//` comment, trailing commas after the custom object and its arrays, and a `value-delivery` custom scenario whose `systemPrompt` was an **array of 5 lines** (with built-ins disabled). On the fresh-launch tick, `scenarios.log` recorded `scenario=value-delivery` running, and the logged `--- system prompt ---` was the **joined multi-line text** (all five lines, including the blank line, joined with newlines). This proves both that the annotated config parsed (it did not silently fall back — the built-ins were disabled and only the custom ran) and that the array was joined into one prompt. The scenario then **emitted** a real, trail-grounded nudge tagged `value-delivery`.

**Backward compatible** — a plain-string `systemPrompt` returns unchanged (`StrOrLines` yields the string directly for a `String` element; the previous change's tests already emitted from a string prompt), and removing the test config returns the app to the built-in set with no config file.
