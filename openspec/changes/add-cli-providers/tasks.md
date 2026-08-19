## 1. Config + provider abstraction

- [x] 1.1 Add a `HuddleConfig` loader for `huddle.config.json` (resolves exe dir then `%LOCALAPPDATA%\Huddle\`): `{ Provider (claude|copilot|agency), Command, Model?, CaptureDenylist[] }`. Missing file → default `claude`, empty denylist. Gitignore `huddle.config.json`.
- [x] 1.2 Define `ICliProvider` with `CompleteAsync(ScenarioRequest, ct) → BackendResult` and `DescribeImageAsync(string imagePath, string prompt, ct) → string?`.
- [x] 1.3 `CliProviderFactory.Resolve()` reads `HuddleConfig` and returns the concrete provider (`agency` → Copilot class with the configured command).

## 2. Local model/effort types; drop the Anthropic SDK

- [x] 2.1 Replace `ScenarioRequest.Model` (Anthropic `Model`) with a plain `string` model name; replace `Effort` with a local `Huddle.Scenarios.Effort` enum.
- [x] 2.2 Update scenarios (Learnings/Achievements/LinkedIn/Efficiency) and `ApiBackend` removal to the new types; scenario `ModelId` becomes a model-name string.
- [x] 2.3 Remove `ApiBackend.cs` and the `Anthropic` PackageReference from `Huddle.App.csproj`. Confirm nothing else imports the SDK.

## 3. Claude provider

- [x] 3.1 Refactor the existing `CliBackend` into `ClaudeCliProvider : ICliProvider`. `CompleteAsync` keeps today's behavior (stdin prompt, `--append-system-prompt`, `--model`, `--effort` + adaptive thinking, plain-text stdout, UTF-8, key-scrub, `--tools WebSearch WebFetch --dangerously-skip-permissions` when `WebSearch`).
- [x] 3.2 `DescribeImageAsync`: `claude -p "<prompt> @<imagePath>" --model sonnet`, UTF-8 stdout, return the text.

## 4. Copilot/Agency provider

- [x] 4.1 `CopilotCliProvider : ICliProvider` (command from config, default model `claude-opus-5`). `CompleteAsync`: concatenate `SystemPrompt` + schema directive + `UserText`, **write to a temp `.md`**, invoke `<command> -p "Read the file at <path> and follow its instructions" --allow-tool=read --add-dir <tempDir> -s --model <model> --no-ask-user`, delete the temp file (finally), return stdout (parse as `NudgeDraft`); non-zero exit → null. Ignore `Effort`.
- [x] 4.2 Verified approach (D5): the temp-`.md` + `--allow-tool=read` path handles Learnings' full ~64K prompt (Copilot ignores stdin and won't attach text; the `read` tool loads the file). No trail cap needed. Keep `--allow-tool=read` narrow (never `--allow-all-tools`). **Found & fixed during verification:** even with `-s`, Copilot prefaces the read-tool run with prose ("I'll read the file."), so `CompleteAsync` isolates the first balanced JSON object (`ExtractJsonObject`, string-aware brace scan) before returning — the Claude path already returns clean JSON.
- [x] 4.3 `DescribeImageAsync`: `<command> -p "<prompt>" --attachment <imagePath> -s --model <model> --no-ask-user`, return stdout (vision output is clean — no `read` tool, no preamble).
- [x] 4.4 Web search (D6): when `WebSearch`, add `--allow-tool=url` (narrow grant, never `--allow-all-tools`). **Still to verify on the work machine:** whether `url` truly searches or only fetches; if it can't search, Efficiency Insights runs fetch-only/ungrounded or is skipped — never fabricate a citation.

## 5. Vision via the provider (moment-capture)

- [x] 5.1 Rewrite `MomentExtractor` to build the vision prompt (recent-moments block + foreground block, as today) and call `provider.DescribeImageAsync(tempPath, prompt)` instead of the Anthropic SDK.
- [x] 5.2 Write the resized screenshot to a temp `.jpg`; delete it in a `finally` after the call (ephemeral — only the summary is stored).
- [x] 5.3 In the tick path, check the foreground app/title against `CaptureDenylist` before capturing; on a match, skip the tick (no screenshot, no moment).
- [x] 5.4 Capture scope setting (`captureScope`: `fullScreen` default | `activeWindow`). Full-screen keeps `BitBlt` of the primary display; active-window uses `PrintWindow(PW_RENDERFULLCONTENT)` so only the foreground window's own pixels are captured, making the denylist an exact guarantee. The tick passes `HuddleConfig.Current.CaptureActiveWindowOnly` to `ScreenCapture.CaptureAsJpegAsync`.

## 6. Wire scenarios to the provider

- [x] 6.1 Replace `ScenarioBackendFactory`/`Backend` usage so scenarios resolve the provider via `CliProviderFactory`. Efficiency Insights keeps its single web-search call through the provider (dropped its hardcoded `CliBackend`; now uses the configured `Provider` with `WebSearch: true`).

## 7. Verify

- [x] 7.1 `dotnet build Huddle.slnx -c Debug` clean; no `Anthropic` package remains.
- [x] 7.2 Claude provider (default config, personal machine): app launches, the immediate first tick produced a moment via `claude` vision (temp `.jpg` gone afterward), and the Learnings scenario emitted a real nudge via the Claude provider.
- [x] 7.3 Copilot provider (`huddle.config.json` provider=copilot): `copilot` is installed on this machine, so verified live — a capture tick produced a moment via `copilot --attachment`, and a scenario ran via the temp-`.md` + `--allow-tool=read` path returning clean `NudgeDraft` JSON (after the preamble fix in 4.2).
- [~] 7.4 Ephemeral verified (temp `.jpg`/`​.md` count returns to 0 after normal runs; a force-kill mid-call can orphan one temp file, cleaned by the OS). Denylist: substring-match logic implemented in the tick and compiles; not exercised live (controlling the foreground window at tick time is impractical here) — low risk.
- [x] 7.5 Record commands and outcomes in tasks.md §Verification.

## Verification

Verified on the personal machine (2026-08-18). `copilot` turned out to be installed here (WinGet), so **both** providers were exercised live, not just Claude.

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`. The `Anthropic` PackageReference is gone; a source scan for `using Anthropic` / `IScenarioBackend` / `CliBackend` / `ApiBackend` / `ScenarioBackendFactory` finds only the intended `ANTHROPIC_API_KEY` scrub comment in `ClaudeCliProvider`.

**Claude provider (default, no config file)** — launched the app; the immediate first tick wrote a moment through `MomentExtractor → ClaudeCliProvider.DescribeImageAsync`:
> `chrome.exe / "Payments | PEMCO Insurance"` → *"You're reviewing your PEMCO insurance payment details — likely checking when the next auto insurance payment of $252 is due (08/27/2026)..."*

`huddle.db` write time jumped to now; a real Learnings nudge was emitted via the Claude scenario path ("When a feature is gated on a cross-cutting refactor, land the refactor as its own behavior-preserving PR first."). The direct vision command `claude -p "<prompt> @<jpg>" --model sonnet` returned exit 0 with an intent-framed summary.

**Copilot provider (`{ "provider": "copilot" }` in the exe dir)** — restarting picked up the config (loader + defaults: command `copilot`, model `claude-opus-5`), the factory switched to `CopilotCliProvider`, and the tick wrote a moment via `copilot -p "<prompt>" --attachment <jpg> -s --model claude-opus-5 --no-ask-user`:
> *"You're still on the PEMCO payments page, scrolled to the Auto policy #CA 1851695 — likely confirming the $1,251.97 remaining balance..."*

The scenario path (temp `.md` + `copilot -p "Read the file at <path>..." -s --model claude-opus-5 --no-ask-user --allow-tool=read --add-dir <dir>`) returned a clean `NudgeDraft` JSON in `scenarios.log` (`{"emit": false, "reason": ...}`) with no preamble.

**Bug found via verification** — a direct Copilot scenario run showed that even with `-s`, Copilot prefaces the read-tool run with `"I'll read the file."` before the JSON. `CompleteAsync` now isolates the first balanced JSON object (`ExtractJsonObject`) so the scenario deserializer sees clean JSON; confirmed by the clean logged response above. Vision output has no such preamble (no `read` tool) and is left untouched.

**Ephemeral / cleanup** — temp `huddle-frame-*.jpg` count returns to 0 after each vision call, and `huddle-prompt-*.md` after each scenario call; the only orphan seen came from force-killing the app mid-call (managed `finally` can't run on external kill) and is OS-transient.

**Capture scope setting** — added after review showed the denylist (foreground-only) and capture (full-screen) inspected different things. `captureScope: activeWindow` verified live: a tick captured the foreground Claude window via `PrintWindow(PW_RENDERFULLCONTENT)` and produced a summary that read the window's real on-screen text (named `ScreenCapture.cs`, `PrintWindow`, the PR) — i.e. only that one window, not the full desktop. A separate tick on Huddle's own transparent panel produced a "blank frame" summary (documented acrylic quirk), confirming the branch grabs the *active* window. Default stays `fullScreen` (unchanged personal-machine behavior).

**Not exercised live** — the denylist skip itself (substring match compiles and is wired into the tick; controlling the foreground at tick time is impractical here), and whether Copilot's `url` tool truly *searches* vs only *fetches* for Efficiency Insights (D6) — deferred to the work machine.
