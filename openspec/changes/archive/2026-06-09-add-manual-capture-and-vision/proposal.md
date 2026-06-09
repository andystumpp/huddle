## Why

The Activity tab currently shows seeded patterns — useful for locking the visual contract, but the whole point of Huddle is for those observations to come from *what's actually on your screen*. The smallest valuable end-to-end slice is a manual trigger that captures the current screen + foreground window, sends both to Claude with a moment-extraction prompt, and writes the resulting summary to a log. No tick loop, no SQLite, no UI swap. That validates the hardest unknowns — Windows capture, API key handling, the vision-call cost and response shape, prompt voice — without coupling them to the scheduler, the store, or the panel.

Next iterations (in this order) will:
1. Persist moments to SQLite and read them back into the Activity tab (replaces `PatternSeed`).
2. Add the 3-min tick loop (per ADR 0001) so capture happens automatically.
3. Layer scenario prompts on top of moments to produce nudges.

## What Changes

- Add a temporary **"Snapshot now"** button to the Activity tab. It triggers a single screen capture + Claude vision call when clicked.
- On click:
  1. Capture the primary display (Win32 BitBlt), resize to ≤ 1280 × 720, encode as JPEG (~80% quality).
  2. Read the foreground window's process name and window title.
  3. Send the image + a moment-extraction prompt to Claude via the official `Anthropic` C# SDK.
  4. Append the response — a 1-2 sentence "moment summary" — to a log file at `%LOCALAPPDATA%\Huddle\moments.log`, along with the timestamp, app name, and window title.
- API key comes from the `ANTHROPIC_API_KEY` environment variable. If the variable isn't set, the button shows an error toast (built into the panel header, not a separate window) and writes nothing.
- Model: `claude-sonnet-4-6`. Best speed / intelligence balance for vision summarization ($3 / $15 per MT). Max output: 250 tokens.
- The captured image is held only in memory for the API call; it's **not** persisted to disk. Per ADR 0001 the frame is discarded after the call.
- Nothing in the Activity tab UI changes beyond adding the button. The seeded patterns continue to render; the moment summaries land in the log file, not the panel.

## Capabilities

### New Capabilities
- `moment-capture`: the capture + Claude-vision pipeline. Owns the trigger affordance (currently a button — later replaced by the tick loop), the Windows capture path, the call to Claude, the moment schema, and the local log sink.

### Modified Capabilities
- `app-shell`: adds the temporary **Snapshot now** button to the Activity tab. This is a deliberately throwaway affordance — removed when the tick loop lands. We're marking it as a `moment-capture` requirement so it goes away cleanly later, but it lives on the panel UI for now.

## Impact

- Add `src/Huddle.App/Capture/ScreenCapture.cs` — Win32 BitBlt + `BitmapEncoder` (Windows.Graphics.Imaging) to produce JPEG bytes.
- Add `src/Huddle.App/Capture/ForegroundContext.cs` — `GetForegroundWindow` + `GetWindowText` + `GetWindowThreadProcessId` + process-name lookup.
- Add `src/Huddle.App/Vision/MomentExtractor.cs` — wraps the official `Anthropic` SDK; takes JPEG bytes + foreground context + a system-prompt string, returns the model's text response.
- Add `src/Huddle.App/Vision/Moment.cs` — `record Moment(string Id, DateTimeOffset Ts, string App, string WindowTitle, string Summary)`.
- Add `src/Huddle.App/Vision/MomentLog.cs` — `AppendAsync(Moment)` writing JSON-Lines to `%LOCALAPPDATA%\Huddle\moments.log`.
- Update `src/Huddle.App/Views/PeekPanelWindow.xaml(.cs)` — add the Snapshot button in the Activity section header (small, right-aligned, no text — just a camera glyph). Wire its click handler to the capture pipeline.
- Update `src/Huddle.App/Huddle.App.csproj` — add `<PackageReference Include="Anthropic" Version="..." />`.
- No SQLite, no tick loop, no UI display of moments, no scenario calls. The moments only appear in the log file.
