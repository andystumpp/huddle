## 1. Project setup

- [x] 1.1 Add `<PackageReference Include="Anthropic" Version="*" />` to `src/Huddle.App/Huddle.App.csproj` and confirm `dotnet restore` succeeds
- [x] 1.2 Verify the exact symbol names for `ImageBlockParam`, `Base64ImageSource`, `Model.ClaudeSonnet4_6`, and `TextBlock` against the installed package version; adjust `MomentExtractor` accordingly if they differ from `design.md` D9

## 2. Capture pipeline

- [x] 2.1 Add `src/Huddle.App/Capture/ScreenCapture.cs` exposing `static Task<byte[]> CaptureAsJpegAsync(int maxLongEdge = 1280, int qualityPercent = 80)`
- [x] 2.2 Implementation: P/Invoke `GetDC(IntPtr.Zero)` + `CreateCompatibleDC` + `CreateCompatibleBitmap` + `BitBlt` + `GetDIBits` to grab the primary display into a `byte[]` (BGRA)
- [x] 2.3 Use `SoftwareBitmap.CreateCopyFromBuffer(...)` to wrap the bytes, then a `BitmapEncoder` (`BitmapEncoder.JpegEncoderId`) with `BitmapTransform.ScaledWidth/Height` set so the longest edge ≤ `maxLongEdge`
- [x] 2.4 Encode at `qualityPercent` quality via the JPEG `BitmapPropertySet` (`ImageQuality`)
- [x] 2.5 Wrap the whole thing in try/catch — on failure throw a `ScreenCaptureException` with the inner exception preserved

## 3. Foreground context

- [x] 3.1 Add `src/Huddle.App/Capture/ForegroundContext.cs` with `record ForegroundInfo(string App, string WindowTitle)` and `static ForegroundInfo Read()`
- [x] 3.2 P/Invoke `GetForegroundWindow` + `GetWindowText` (Unicode, with `GetWindowTextLength` for the buffer) + `GetWindowThreadProcessId`
- [x] 3.3 Resolve process name via `Process.GetProcessById(pid).ProcessName + ".exe"` inside a try/catch
- [x] 3.4 Fall back to `("Unknown", "")` if `GetForegroundWindow` returns `IntPtr.Zero` or process lookup throws

## 4. Moment model + log sink

- [x] 4.1 Add `src/Huddle.App/Vision/Moment.cs` — `record Moment(string Id, DateTimeOffset Ts, string App, string WindowTitle, string Summary)` plus a sibling `record MomentLogEntry` that includes an optional `string? Error` for failure rows
- [x] 4.2 Add `src/Huddle.App/Vision/UlidGenerator.cs` — tiny static `Generate()` returning a Crockford-base32-style 26-char string from `DateTimeOffset.UtcNow` and a `RandomNumberGenerator` (no external dependency)
- [x] 4.3 Add `src/Huddle.App/Vision/MomentLog.cs` exposing `static Task AppendSuccessAsync(Moment)` and `static Task AppendFailureAsync(string app, string windowTitle, string errorMessage)`
- [x] 4.4 Both methods serialize a single object via `System.Text.Json.JsonSerializer.Serialize(...)` with `JsonSerializerOptions.WriteIndented = false`, append `'\n'`, and write to `%LOCALAPPDATA%\Huddle\moments.log` (create the directory if missing)

## 5. Vision call

- [x] 5.1 Add `src/Huddle.App/Vision/MomentExtractor.cs` with `Task<string> ExtractAsync(byte[] jpegBytes, ForegroundInfo foreground, CancellationToken ct = default)`
- [x] 5.2 Construct a single-shot `AnthropicClient` (let it read `ANTHROPIC_API_KEY` from the env). Cache the client in a static field so we're not re-instantiating per click
- [x] 5.3 Build the system prompt from `design.md` D6 as a private const string
- [x] 5.4 Build the request: `Model.ClaudeSonnet4_6`, `MaxTokens = 250`, `System = SystemPrompt`, one user `MessageParam` with `[ImageBlockParam(base64), TextBlockParam("Foreground app: …\nWindow title: …")]`. The skill notes `Model.ClaudeOpus4_8` as the default — we are deliberately picking Sonnet here (see `design.md` D5)
- [x] 5.5 Convert `jpegBytes` to base64 via `Convert.ToBase64String(jpegBytes)`
- [x] 5.6 Call `await client.Messages.Create(parameters)`; pick the first `TextBlock` from `response.Content` and return its `Text`. Throw `InvalidOperationException("Empty response")` if no text block is present
- [x] 5.7 Wrap any thrown exceptions in a `VisionCallException` whose message includes the SDK's exception type for debugging

## 6. Trigger button + wiring

- [x] 6.1 In `PeekPanelWindow.xaml`, add a `Button` (28 × 28, `CornerRadius=7`, transparent background, camera glyph as a `Path`) at the right edge of the "PATTERNS DETECTED N" section-header row; bind `Click="OnSnapshotClick"`
- [x] 6.2 Camera glyph `Path` data: a rounded rectangle outline with a small circle inside; stroke = `T2` brush at 1.4 px
- [x] 6.3 Add a `TextBlock x:Name="SnapshotStatusText"` next to the count, default `Visibility="Collapsed"`. Used to flash status messages
- [x] 6.4 In `PeekPanelWindow.xaml.cs`, add `async void OnSnapshotClick(object, RoutedEventArgs)`:
  - Disable the button + dim it (via `IsEnabled = false` and `Opacity = 0.4`)
  - Read API key existence (`Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")`); if missing, call `ShowSnapshotStatus("Set ANTHROPIC_API_KEY", error: true)` and `MomentLog.AppendFailureAsync(...)`; return
  - `try { var foreground = ForegroundContext.Read(); var jpeg = await ScreenCapture.CaptureAsJpegAsync(); var summary = await MomentExtractor.ExtractAsync(jpeg, foreground); await MomentLog.AppendSuccessAsync(new Moment(UlidGenerator.Generate(), DateTimeOffset.UtcNow, foreground.App, foreground.WindowTitle, summary)); ShowSnapshotStatus("Saved", error: false); } catch (Exception ex) { ShowSnapshotStatus($"Error: {ex.Message}", error: true); await MomentLog.AppendFailureAsync(foregroundAppOrUnknown, foregroundTitleOrEmpty, ex.Message); } finally { Re-enable button }`
- [x] 6.5 `ShowSnapshotStatus(string text, bool error)`: set `SnapshotStatusText.Text`, foreground color = `error ? red-ish : efficiency teal`, `Visibility = Visible`. Use a `DispatcherTimer` (3 s, single shot) to hide it again

## 7. Verification

- [x] 7.1 `dotnet build Huddle.slnx -c Debug` succeeds with 0 warnings, 0 errors
- [x] 7.2 Launch the app — camera button is visible at the right edge of the "PATTERNS DETECTED 4" header
- [x] 7.3 With `ANTHROPIC_API_KEY` set, click the button → after ≤ a few seconds, the status text reads "Saved", and a new line appears in `%LOCALAPPDATA%\Huddle\moments.log` with the expected schema
- [x] 7.4 Open the log file — confirm the summary reads as a 1-2 sentence second-person observation about whatever was on screen
- [x] 7.5 With `ANTHROPIC_API_KEY` unset, click the button → status reads "Set ANTHROPIC_API_KEY", an error line is appended to the log, no API call is made
- [x] 7.6 Confirm no `.jpg` / `.png` files appear anywhere under `%LOCALAPPDATA%\Huddle\`
- [x] 7.7 Click the button repeatedly — it disables during the call and re-enables when done; clicking again immediately works
