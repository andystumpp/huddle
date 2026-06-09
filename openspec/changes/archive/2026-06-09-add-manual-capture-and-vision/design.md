## Context

ADR 0001 settles the end-to-end pipeline: every 3 min, capture a frame + foreground window context, send to Claude, get back a moment summary, write to SQLite. This change is the **smallest end-to-end slice of that pipeline**: just the vision call, triggered manually, with the result going to a log file instead of SQLite. That validates the hardest unknowns — Windows capture APIs, API key handling, vision-call cost and response shape, prompt voice — before we wire in the tick scheduler or the store.

The previous iterations gave us the shell (acrylic panel, look-bar, tabs) and the Activity tab surface (seeded patterns). This iteration says: here's what the *content* will actually look like when it comes from the model. The seeded patterns stay on the Activity tab; the new moment summaries land in a log file we read with a text editor.

## Goals / Non-Goals

**Goals:**

- A single manual trigger (a small button in the Activity tab section header) captures the primary display and the foreground app/title.
- A Claude vision call returns a 1-2 sentence moment summary in the prototype voice ("dry, observant kibitzer; second-person, specific").
- The summary is appended to `%LOCALAPPDATA%\Huddle\moments.log` as JSON-Lines, with `id`, `ts`, `app`, `window_title`, `summary` — matching ADR 0001's moment schema.
- The captured frame is held in memory only and discarded after the call.
- API key resolution: `ANTHROPIC_API_KEY` env var. Missing → graceful error (in-panel toast + log entry), not a crash.
- Cost per snapshot stays modest (~$0.006 at Sonnet 4.6, ~1,800 input + ~80 output tokens).

**Non-Goals:**

- No tick loop — the only trigger this change ships is the manual button.
- No SQLite — moments go to a log file. The store lands in a later change.
- No scenario calls / nudges — we're only producing moments.
- No UI display of moments — the Activity tab still shows the seeded patterns.
- No frame retention — the captured image is not saved to disk.
- No multi-monitor support — primary display only.
- No prompt caching — single-shot prompts; caching is for tick-loop iterations.
- No streaming — we just await the full response.
- No retries beyond the SDK's default (`max_retries=2`).
- No detection of "boring" frames (lock screen, empty desktop). User's call to press the button.

## Decisions

### D1. Official `Anthropic` C# SDK over hand-written HTTP

- **Choice:** Add `<PackageReference Include="Anthropic" Version="*" />` and call `client.Messages.Create(...)`.
- **Rationale:** The `claude-api` skill is explicit: use the official SDK for languages that have one. We do (`Anthropic` on NuGet). Hand-written HTTP would mean re-inventing the message shape, retry logic, error handling, and structured types — for negative gain. The SDK also gives us typed `Model.ClaudeSonnet4_6`, `Base64ImageSource`, etc., which means we'd catch model-string typos at compile time.
- **Alternative considered:** Raw `HttpClient` POST to `/v1/messages`. Rejected — zero upside, several days of avoidable bugs.

### D2. API key from `ANTHROPIC_API_KEY` env var

- **Choice:** Read at startup. The SDK does this automatically via `new AnthropicClient()`. If the env var is missing, the Snapshot button is still clickable but each click logs an error and shows a tooltip-style error in the panel header for ~3 s. No crash.
- **Rationale:** Andy explicitly chose this option ("a) also good"). Zero-config for dev, zero plumbing in the app, matches what every CLI in the ecosystem expects.
- **Future:** When we ship, this moves to DPAPI-encrypted file or Windows Credential Manager — that's a later change with a real installer story.

### D3. Capture: Win32 BitBlt + `BitmapEncoder`

- **Choice:** P/Invoke `GetDC(NULL)` + `BitBlt` to copy the primary display into a HBITMAP, then `GetDIBits` into a managed `byte[]` (BGRA), wrap that with `SoftwareBitmap.CreateCopyFromBuffer(...)`, encode to JPEG via `BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ...)`. Resize to a max of 1280 × 720 before encoding using `BitmapTransform.ScaledWidth/Height`.
- **Rationale:**
  - `System.Drawing` is unsupported on .NET 10 and unavailable in WinUI 3 apps without extra packages.
  - `Windows.Graphics.Capture` (the API ADR 0001 specifies) is the right long-term answer — but for a *manual one-shot* it's more setup (`GraphicsCaptureItem.CreateFromVisual` / picker / `Direct3D11CaptureFramePool`) than this iteration warrants. The picker UI flashes; the direct-monitor path requires interop to `IDXGIOutput` or `Windows.Graphics.Capture.GraphicsCapturePicker`.
  - BitBlt is ~30 lines of P/Invoke, well-trodden, and handles multi-monitor (we capture the primary). It's the standard fallback in every screen-capture tutorial.
  - We'll swap to `Windows.Graphics.Capture` in the change that introduces the tick loop, where the higher setup cost pays for itself across many frames per session.
- **Resize target:** 1280 × 720. At Haiku 4.5 this is ~1,300–1,600 input tokens — enough for the model to read app UI, far less than full 1080p (~2,000+ tokens). We're optimizing for "good enough at $0.002 per call."
- **JPEG quality:** 80%. Trades a few KB for a small visual fidelity hit; Claude's vision is robust to JPEG artifacts at that quality.

### D4. Foreground context: native Win32 only

- **Choice:** `GetForegroundWindow()` + `GetWindowText()` + `GetWindowThreadProcessId()` + `Process.GetProcessById(pid).ProcessName + ".exe"` for the app key, full window title verbatim.
- **Rationale:** Both fields are deterministic; we don't need vision to read them. ADR 0001 calls this out: app and window_title come from the OS, summary comes from Claude. Matches the prototype's `APP_META` keys (`Code.exe`, `Chrome.exe` minus path).
- **Fallback:** If `GetForegroundWindow` returns NULL or the process can't be queried (rare — usually a permission issue), use `app = "Unknown"`, `window_title = ""` and send anyway.

### D5. Model: `claude-sonnet-4-6`

- **Choice:** Use `Model.ClaudeSonnet4_6` from the SDK.
- **Rationale:** Vision summarization on a real workday screen benefits from better scene understanding than Haiku gives — Sonnet reads dense UIs more reliably and picks the right detail to anchor the sentence on. At $3 / $15 per MT, a ~1,800-input + ~80-output snapshot is ~$0.006 — cheap enough at manual cadence. If we later move to a 3-minute tick (20 calls/hour) cost is still ~$0.12/hour; we can downshift to Haiku then if it earns its keep. Don't reach for Opus on this — the observations are short and Opus's value is in long-horizon agentic work, not single vision calls.
- **`max_tokens`:** 250. A sentence is ~15-25 tokens; 250 leaves ample room without paying for overruns. The skill notes recommend not lowballing, but short outputs like this are an explicit exception.

### D6. Moment-extraction prompt

The system prompt is short on purpose. We're not asking for analysis — just the observation:

> You are Huddle's eye. You see one screenshot of the user's screen plus the name of the app and window in the foreground. Write a single 1-2 sentence observation about what the user is doing right now.
>
> Voice:
> - Dry. Observant. Specific. Second-person.
> - No greetings, no "I see", no "looks like".
> - Anchor in concrete details from the screen — not generic statements.
> - If nothing useful is happening, say so plainly in one sentence.
>
> Do not propose what to do about it. Just describe what's happening.

The user content is the image followed by a short text block:

> Foreground app: {app}
> Window title: {windowTitle}

This is the *moment* prompt from ADR 0001. Scenario prompts (which decide whether to emit a nudge) are a separate, later layer.

### D7. Log file format: JSON-Lines

- **Choice:** Each line is a single JSON object: `{"id": ulid, "ts": "...", "app": "...", "window_title": "...", "summary": "..."}`. Use `System.Text.Json` with `JsonSerializer.Serialize(moment)`.
- **Rationale:** Trivially appendable, trivially diffable, trivially readable in any text editor. Survives the eventual move to SQLite — we'll just `INSERT` whatever the log already has.
- **Path:** `%LOCALAPPDATA%\Huddle\moments.log`. We already write `startup-error.log` and `panel.log` there; same parent dir.
- **No rotation.** This is a prototype log. Andy can delete it whenever. The tick-loop change will move persistence to SQLite anyway.

### D8. Trigger UI: small icon button in the Activity tab section header

- **Choice:** Add a 28×28 icon button to the right of the **"PATTERNS DETECTED N"** section header — a camera glyph (a rounded rect with a small circle inside). Click → capture pipeline. When in-flight, the icon dims and the button is disabled. On success/failure, brief inline status (color-coded text "Saved" / "Error: …") replaces the section header's count for ~3 s.
- **Rationale:** Lives where it logically belongs (the patterns/activity surface) without disrupting the header. Discoverable, removable. Designed-to-die — this whole control gets deleted when the tick loop ships.
- **Why not the settings flyout?** No settings flyout exists yet; building it is a separate change. A bare button is the smallest thing.

### D9. SDK call shape (forward-looking but locked)

The Anthropic SDK for C# accepts `MessageCreateParams` with `Messages` taking a `List<MessageParam>`. For vision the user message's `Content` is a `List<ContentBlockParam>` containing an `ImageBlockParam` (with `Base64ImageSource`) followed by a `TextBlockParam`:

```csharp
var response = await client.Messages.Create(new MessageCreateParams
{
    Model = Model.ClaudeSonnet4_6,
    MaxTokens = 250,
    System = SystemPrompt,
    Messages = new List<MessageParam>
    {
        new()
        {
            Role = Role.User,
            Content = new List<ContentBlockParam>
            {
                new ImageBlockParam
                {
                    Source = new Base64ImageSource
                    {
                        Data = base64Jpeg,
                        MediaType = "image/jpeg",
                    },
                },
                new TextBlockParam
                {
                    Text = $"Foreground app: {app}\nWindow title: {title}",
                },
            },
        },
    },
});

var summary = response.Content
    .Select(b => b.Value)
    .OfType<TextBlock>()
    .Select(t => t.Text)
    .FirstOrDefault() ?? string.Empty;
```

If the exact SDK type names (`ImageBlockParam` / `Base64ImageSource`) differ when we install the latest package, we adjust — the shape is the same and the SDK README will name them. We'll verify against the installed version during implementation.

## Risks / Trade-offs

- **[BitBlt fails or returns black on some display configurations — most often when a foreground app uses HDR / hardware overlays / DRM protected content (e.g. Netflix in Edge)]** → Accepted. Surface a friendly error in the panel header and log the failure. The tick-loop change will switch to `Windows.Graphics.Capture` which handles these cases via the Graphics Capture session model.
- **[Foreground process is elevated (admin) and our app is not]** → `GetWindowText` works across the elevation boundary, but `OpenProcess` for `GetProcessImageFileName` fails. Fallback to "Unknown" for the app key. Acceptable — those windows are a small fraction of normal work.
- **[Andy hasn't set `ANTHROPIC_API_KEY`]** → First click writes an error log entry and surfaces a 3-second header status: "Set ANTHROPIC_API_KEY". No crash.
- **[Image with personally identifiable info (visible passwords, private messages) goes to Anthropic's API]** → This is exactly the data ADR 0001 designed around: frames are discarded after the call, only the summary persists. We note this in the README during a later docs pass; for prototype-only use, it's an accepted trade-off.
- **[Network is offline]** → SDK throws; we catch and log. No retry beyond the SDK's default.
- **[Moment quality at Sonnet 4.6 might still miss the point on dense screens]** → The upgrade is one constant to `Model.ClaudeOpus4_8` (~5× cost). The downgrade if cost matters more than quality is `Model.ClaudeHaiku4_5` (~⅓ cost). Iterate based on what the log shows.

## Open Questions

- When the tick loop lands, the manual button gets deleted. Should we keep it as a "force snapshot now" affordance for testing forever, or fully retire it? Current plan: delete it. Restart the app, the tick fires within 3 min.
- Do we want any sanity check on the image bytes before sending (e.g., is it all-black)? Current plan: no. The model can say "nothing useful is happening" on a black frame, and the cost is the same.
- Resize target — 1280×720 is a guess. If observations are unreliable we may push to 1600×900 (~2,000 tokens / ~$0.003 per call). Try the default first; reassess after a dozen real snapshots.
