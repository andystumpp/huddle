## 1. Prompt rewrite

- [x] 1.1 In `src/Huddle.App/Vision/MomentExtractor.cs`, replace the `SystemPrompt` constant with the intent-framed version from `design.md` D1 (raw string literal; preserve indentation as written)

## 2. Trail plumbing

- [x] 2.1 Change `MomentExtractor.ExtractAsync`'s signature to `(byte[] jpegBytes, ForegroundInfo foreground, IReadOnlyList<Moment> recent, CancellationToken ct = default)`
- [x] 2.2 If `recent.Count == 0`, the user message's content list is `[ImageBlockParam, TextBlockParam("Foreground app: {app}\nWindow title: {title}")]` — i.e., no trail block (matches today's behavior)
- [x] 2.3 If `recent.Count > 0`, build a single `TextBlockParam` whose text is `"Recent moments (newest first):\n" + entriesJoinedByNewline + "\n\nForeground app: {app}\nWindow title: {title}"`
- [x] 2.4 Each entry uses the format from `design.md` D3: `"- {relTime}, {app} (\"{titleAbbrev}\"): {summary}"`. Provide a small static helper `FormatRelativeTime(DateTimeOffset ts)` — outputs `"just now"`, `"{n} min ago"` if `n < 60`, else `"{n} h ago"` with hours rounded down. Truncate window titles to 80 chars (split on whitespace where possible; otherwise hard-cut)
- [x] 2.5 Normalize each `recent[i].Summary` by replacing CR/LF with single spaces and collapsing runs of whitespace, so the prompt stays readable

## 3. Wire from the panel

- [x] 3.1 In `PeekPanelWindow.OnSchedulerTick`, before the existing call to `ScreenCapture.CaptureAsJpegAsync`, call `var recent = await MomentStore.RecentAsync(6);` and capture the result
- [x] 3.2 Pass `recent` through to `MomentExtractor.ExtractAsync(jpeg, foreground, recent)`
- [x] 3.3 The new moment is still constructed + persisted + prepended to the panel collection as it is today — no change to that sequence

## 4. Verification

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 4.2 Launch on a fresh database (`del %LOCALAPPDATA%\Huddle\huddle.db*`) — first moment lands, no trail in the prompt, summary still reads as intent
- [x] 4.3 Wait through 2–3 ticks — subsequent moments visibly reference what the user has been doing across captures, not just the single frame
- [x] 4.4 Inspect a captured moment's `summary` in `huddle.db` — the phrasing matches the new voice (intent-framed, hedged when appropriate). Spot-check at least three back-to-back moments
- [x] 4.5 Pivot mid-session — switch foreground app to something unrelated and let the next tick fire; the new moment should pivot too (no goal stickiness)
- [x] 4.6 Confirm cost-per-call is in the expected range (~$0.008) by reading SDK usage telemetry on a debug log line — optional, only if quick
