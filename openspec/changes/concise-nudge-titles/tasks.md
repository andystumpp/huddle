## 1. Constrain the shared schema

- [x] 1.1 In `ScenarioPromptHelpers.BuildNudgeDraftSchema()`, give the `title` property a `description` bounding it to a short plain-language concept (~8 words, name the idea/outcome, no full sentences, no code identifiers/filenames/PR numbers/CLI commands), and give the `body` property a `description` directing the specifics, evidence, named sources, and any URLs there. No properties added/removed; `required` stays `["emit"]`.

## 2. Verify

- [x] 2.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 2.2 Confirm the new descriptions reach the model. (Note: the schema directive is appended *inside* the provider and is not written to `scenarios.log`, so the log can't show it — but `BuildSchemaDirective` serializes the whole schema, descriptions included, into the prompt by construction; confirmed downstream by 2.3.)
- [x] 2.3 Sanity-check emitted titles on a live tick: emitted title reads as a short concept, with the detail in the body. Record before/after examples.
- [x] 2.4 Record commands and outcomes in §Verification.

## Verification

Ran on the real machine (`%LOCALAPPDATA%\Huddle`), provider `claude`, 2026-08-23.

**2.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**2.2 — Descriptions reach the model.** The schema directive is built in the provider (`request.SystemPrompt + BuildSchemaDirective(request.JsonSchema)` at [ClaudeCliProvider.cs:28](../../../src/Huddle.App/Scenarios/ClaudeCliProvider.cs:28)), *after* the point `ScenarioDiagnostics.LogRun` captures the system prompt — so it never lands in `scenarios.log`. But `BuildSchemaDirective` does `JsonSerializer.Serialize(schema)`, which serializes the `title`/`body` `description` fields into the prompt by construction. Proven end-to-end by 2.3.

**2.3 — Before/after titles.** Launched the freshly built exe; the first tick ran all four scenarios with the new schema. `achievements` emitted `"Constrained nudge titles to concept level"` — a 6-word concept, no code identifiers. The other three stayed silent (`emit:false`, nothing new — correct). Crucially, `achievements`'s *config prompt is unchanged*; the only difference from prior runs is the schema description, so the shift from run-ons to a concept is attributable to this change alone.

Before (same scenario, prior runs): *"Implemented sensitive-moment detection — both paths comp-tested, binary verification underway"*, *"Your per-key config merge is verified by relaunching Huddle — lock the resolved ConfiguredScenario output with Verify snapshot tests…"*. After: *"Constrained nudge titles to concept level"*.

One emitted title this tick (the others were legitimately silent); broader confirmation accrues as more nudges emit during normal use. If titles still trend long, the follow-up is softening the four `"…in one line"` lines in `huddle.config.json` (config-only, no rebuild).
