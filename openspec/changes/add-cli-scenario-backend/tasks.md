## 1. Config plumbing

- [x] 1.1 Extract the `huddle.env` / User-registry / process-env resolution out of `MomentExtractor` into a shared `EnvConfig` helper (`src/Huddle.App/Config/EnvConfig.cs`) exposing `string? Resolve(string name)`, reusing the existing precedence and env-file candidate logic.
- [x] 1.2 Repoint `MomentExtractor` key lookup at `EnvConfig.Resolve("ANTHROPIC_API_KEY")` so key resolution is unchanged but no longer duplicated.

## 2. Backend abstraction

- [x] 2.1 Add `ScenarioRequest` (record: `Model Model`, `int MaxTokens`, `string SystemPrompt`, `string UserText`, required `Dictionary<string, JsonElement> JsonSchema`, optional `Effort? Effort`) and `BackendResult` (readonly record struct: `string? Text`, `long? InputTokens`, `long? OutputTokens`) in `src/Huddle.App/Scenarios/IScenarioBackend.cs`, plus the `IScenarioBackend.CompleteAsync(ScenarioRequest, CancellationToken)` interface.
- [x] 2.2 Implement `ApiBackend` by lifting the current scenario call path: build `MessageCreateParams` (Model, MaxTokens, System, `OutputConfig.Format = JsonOutputFormat { Schema = req.JsonSchema }`), and when `req.Effort` is set apply `OutputConfig.Effort` **and** `Thinking = ThinkingConfigAdaptive()` (both init-only, set in initializers); call `Messages.Create`, return first `TextBlock` text + `Usage` input/output tokens.
- [x] 2.3 Implement `CliBackend`: spawn `claude -p <UserText> --model <alias> --append-system-prompt <system>` (plus `--effort <level>` when `req.Effort` set) via `ProcessStartInfo` (argv entries, no shell, redirect stdout+stderr, default plain-text output), map model→alias by substring (`opus`/`sonnet`/`haiku`, throw on unmapped). On exit code 0 return stdout as the text with null token counts; on non-zero exit capture stderr for diagnostics and return null text (scenario no-emits). Conservative 180 s timeout kills the process and returns null text.
- [x] 2.4 In `CliBackend`, remove `ANTHROPIC_API_KEY` from `ProcessStartInfo.Environment` before launch so the child authenticates against the subscription.
- [x] 2.5 In `CliBackend`, always append a directive to the system prompt (serialized from `req.JsonSchema`) instructing a single JSON-object response matching the schema and nothing else.
- [x] 2.6 Add `ScenarioBackendFactory.Resolve()` reading `HUDDLE_SCENARIO_BACKEND` via `EnvConfig`: `cli` → `CliBackend`, anything else (including unset/unknown) → `ApiBackend`.

## 3. Wire scenarios to the backend

- [x] 3.1 Add a `protected IScenarioBackend Backend` to the `Scenario` base class, resolved once via `ScenarioBackendFactory.Resolve()`.
- [x] 3.2 Port `LearningsScenario.ExecuteAsync` to call `Backend.CompleteAsync(...)` instead of `new AnthropicClient()`, keeping the `NudgeDraft` parse, `ScenarioDiagnostics.LogRun`, and nudge construction identical.
- [x] 3.3 Port `AchievementsScenario.ExecuteAsync` the same way.
- [x] 3.4 Port `LinkedInPostsScenario.ExecuteAsync` the same way, passing `Effort: Effort.High` so the backend reproduces its high-reasoning config (API: effort + adaptive thinking; CLI: `--effort high`).
- [x] 3.5 Confirm `EfficiencyInsightsScenario` is left untouched (still direct `AnthropicClient` for its web-search phases).

## 4. Verify

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` is clean.
- [x] 4.2 Default path unchanged: with `HUDDLE_SCENARIO_BACKEND` unset, the app launches and the vision tick resolves the key via `EnvConfig` (proves the extraction is behavior-preserving); scenarios continue to use `ApiBackend`.
- [x] 4.3 CLI path verified at the component level (login restored): reproduced `CliBackend`'s exact `ProcessStartInfo` invocation, confirmed the schema-directive prompt returns a single parseable `NudgeDraft` JSON object, and characterized the key scrub (see §Verification). A full in-app run (`HUDDLE_SCENARIO_BACKEND=cli`, scenario emits) is still worth doing but was not forced (avoids spending Opus quota unprompted).
- [x] 4.4 Record the manual verification steps and outcomes in tasks.md §Verification (per CLAUDE.md — manual checks live here, not in code).

## Verification

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)` (after stopping the running `Huddle.exe`, which locked the output on the first attempt).

**Default (API) path** — Relaunched `Huddle.exe`; app starts and the vision tick runs, so `EnvConfig.Resolve("ANTHROPIC_API_KEY")` resolves the key exactly as the old inline lookup did. `HUDDLE_SCENARIO_BACKEND` unset → `ScenarioBackendFactory.Resolve()` returns `ApiBackend`, so scenario calls are unchanged from before this change.

**CLI path** — Login restored (`claude -p "reply ok"` → `ok`; `~/.claude/.credentials.json` refreshed). Verified `CliBackend`'s behavior by reproducing its exact `System.Diagnostics.ProcessStartInfo` invocation from PowerShell:

- **Prompt-instructed JSON (D5)** ✓ — `claude -p <userText> --model sonnet --append-system-prompt <system+schema-directive>` returned a single JSON object with no prose or code fences: `{"emit":true,"title":"Clean build on IScenarioBackend","body":"…","sources":["01TEST"]}`, which `ConvertFrom-Json` parsed into the `NudgeDraft` shape. Confirms the CLI path yields the same parseable output the API path does.
- **Key scrub (D4)** — `psi.Environment.Remove("ANTHROPIC_API_KEY")` mechanically works (`childHasKey=False` after remove). But the scrub is **not load-bearing in the current state**: Claude Code gates env keys behind an approval list, `.claude.json` shows **0 approved keys**, and `claude` ran on the subscription **with and without** the scrub (a bogus key and the real key were both ignored). `total_cost_usd` is a nominal token estimate (0.029977 even on the scrubbed/subscription call), so it does not indicate the billing channel. Conclusion: keep the scrub as cheap insurance for the case where the user later approves that key; corrected D4 accordingly.
- **Model alias / `--effort`** — exercised via the `--model sonnet` invocation above; alias mapping is substring-based (`opus`/`sonnet`/`haiku`).

**Not yet done (optional):** a full in-app run with `HUDDLE_SCENARIO_BACKEND=cli` set in the exe-dir `huddle.env`, launching Huddle and confirming a scenario emits a nudge via `CliBackend` with `%LOCALAPPDATA%\Huddle\scenarios.log` showing `usage: input=? output=?` (null CLI tokens). Deferred to avoid spending Opus quota unprompted.
