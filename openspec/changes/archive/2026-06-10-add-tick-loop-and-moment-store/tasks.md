## 1. Storage

- [x] 1.1 Add `<PackageReference Include="Microsoft.Data.Sqlite" Version="*" />` to `src/Huddle.App/Huddle.App.csproj` and `dotnet restore`
- [x] 1.2 Add `src/Huddle.App/Storage/Database.cs` — opens a connection to `%LOCALAPPDATA%\Huddle\huddle.db` (`Pooling=True`, `Cache=Shared`), enables WAL + `synchronous=NORMAL`, exposes `static Task<SqliteConnection> OpenAsync()` and `static Task InitializeAsync()` that creates the dir and applies migrations
- [x] 1.3 Add `src/Huddle.App/Storage/Migrations/001_init.sql` with the `moments` table + `idx_moments_ts` index per `design.md` D1
- [x] 1.4 Bake the migration into the project as an embedded resource (`<EmbeddedResource Include="Storage\Migrations\*.sql" />` in the csproj) so `Database.InitializeAsync` can read it without a file copy
- [x] 1.5 Track applied migrations in a `__migrations` table; `InitializeAsync` runs only what hasn't been applied yet, in lexical order
- [x] 1.6 Add `src/Huddle.App/Storage/MomentStore.cs` with `AddAsync(Moment)`, `RecentAsync(int limit)`, `CountAsync()`; each method opens / disposes its own connection (the pool keeps it cheap)

## 2. Tick scheduler

- [x] 2.1 Add `src/Huddle.App/Vision/TickScheduler.cs` — `DispatcherTimer` interval 1 s, holds `int SecondsRemaining`, `bool IsPaused`, and an `event EventHandler Tick`
- [x] 2.2 `Start()` raises `Tick` immediately, then sets `SecondsRemaining = 180` and starts the timer
- [x] 2.3 On each timer tick: if `IsPaused`, do nothing; else decrement `SecondsRemaining`. When it reaches 0, raise `Tick` and reset to 180
- [x] 2.4 `Pause()` sets `IsPaused = true` (timer keeps running, but the countdown freezes — same shape the existing fake clock used)
- [x] 2.5 `Resume()` sets `IsPaused = false` and snaps `SecondsRemaining = 180`
- [x] 2.6 `Stop()` stops the timer entirely (for app shutdown)

## 3. Vision pipeline wiring

- [x] 3.1 Move `ResolveApiKey` / `EnvFileCandidates` / `ReadKeyFromEnvFile` from `PeekPanelWindow.xaml.cs` into `MomentExtractor.cs` as private static helpers
- [x] 3.2 In `MomentExtractor.GetOrCreateClient`, call `ResolveApiKey()` first; if it returns non-null, set `Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key)` before instantiating `AnthropicClient` (the SDK reads process env at construction)
- [x] 3.3 If `ResolveApiKey()` returns null, throw `VisionCallException("ANTHROPIC_API_KEY not configured")` so the tick handler catches and skips this cycle

## 4. Moment card (rename + simplify)

- [x] 4.1 Rename `src/Huddle.App/Controls/PatternCard.xaml(.cs)` to `src/Huddle.App/Controls/MomentCard.xaml(.cs)`. Update the `x:Class`, namespace, and constructor name
- [x] 4.2 Change the dependency property from `Pattern` (`Huddle.Models.Pattern`) to `Moment` (`Huddle.Models.Moment`); the property-changed callback now populates `SummaryText.Text = Moment.Summary` and the footer row's tile + title
- [x] 4.3 XAML: drop the bold title `TextBlock` and the multi-tile footer; keep the rounded card, the summary `TextBlock` (now the main body), and a footer `Grid` with an `AppTile` on the left and the window-title `TextBlock` on the right (`TextTrimming="CharacterEllipsis"`)
- [x] 4.4 Move `src/Huddle.App/Models/Moment.cs` to `Huddle.Models` namespace (currently `Huddle.Vision`); update all referencing files. Keeps controls + models in `Huddle.Models` consistent

## 5. Panel surgery

- [x] 5.1 In `PeekPanelWindow.xaml`: change the section-header `TextBlock` text from `"PATTERNS DETECTED"` to `"OBSERVATIONS"`
- [x] 5.2 In `PeekPanelWindow.xaml`: delete the `SnapshotStatusText` `TextBlock` and the `SnapshotBtn` `Button` from the section-header row; the row collapses back to one column carrying just the section title
- [x] 5.3 In `PeekPanelWindow.xaml`: change the `ItemsRepeater` `DataTemplate` to instantiate `controls:MomentCard Moment="{Binding}"` instead of `controls:PatternCard Pattern="{Binding}"`
- [x] 5.4 In `PeekPanelWindow.xaml.cs`: delete `OnSnapshotClick`, `ShowStatus`, `s_errorBrush`, `s_okBrush`, `ResolveApiKey`, `EnvFileCandidates`, `ReadKeyFromEnvFile`. Remove `using Huddle.Capture` (now used only by the orchestration block we add next) — actually keep it, the panel still orchestrates the tick handler
- [x] 5.5 Replace the existing fake tick fields (`_tickTimer`, `TickSeconds = 18`, `_secondsRemaining`) with a `TickScheduler` instance held in a `_scheduler` field; the existing `UpdateStatus` / `UpdateLookBar` methods read `_scheduler.SecondsRemaining` and `_scheduler.IsPaused`
- [x] 5.6 In `OnPauseClick`, call `_scheduler.Pause()` or `_scheduler.Resume()` instead of toggling the local `_paused` flag; keep the icon-swap and the existing `UpdateStatus` / `UpdateLookBar` calls
- [x] 5.7 Add `ObservableCollection<Moment> _moments = new()` as a panel field; bind `MomentsRepeater.ItemsSource = _moments` (rename the existing `PatternsRepeater` to `MomentsRepeater` while we're here)
- [x] 5.8 In `OnContentLoaded`: `await Database.InitializeAsync(); var recent = await MomentStore.RecentAsync(20); foreach (var m in recent) _moments.Add(m); UpdateObservationCount(); _scheduler.Tick += OnTick; _scheduler.Start();`
- [x] 5.9 Add `async void OnTick(object? sender, EventArgs e)`: run the existing capture pipeline (foreground → JPEG → `MomentExtractor.ExtractAsync`), build a `Moment`, `await MomentStore.AddAsync(moment)`, `_moments.Insert(0, moment)`, trim to 20 from the tail, call `UpdateObservationCount()`. Wrap in try/catch — log to `Debug.WriteLine` on failure
- [x] 5.10 Update the section-header count `TextBlock` name to `ObservationCountText` and bind it via `UpdateObservationCount()` to `_moments.Count`

## 6. Cleanup

- [x] 6.1 Delete `src/Huddle.App/Models/Pattern.cs`
- [x] 6.2 Delete `src/Huddle.App/Models/PatternSeed.cs`
- [x] 6.3 Delete `src/Huddle.App/Vision/MomentLog.cs`
- [x] 6.4 Confirm there are no remaining references to `PatternSeed`, `Pattern`, or `MomentLog` in the project

## 7. Verification

- [x] 7.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 7.2 Launch — within a few seconds, a real moment lands in the Activity tab at the top
- [x] 7.3 Section header reads "OBSERVATIONS N" with N reflecting the visible count
- [x] 7.4 Look-bar fills steadily over ~3 minutes; another moment lands when it completes
- [x] 7.5 Click pause — look-bar drops to 0%, status reads "Paused · not watching", no further moments arrive
- [x] 7.6 Click resume — look-bar starts from 0 again, the next tick fires after a full ~3 minutes
- [x] 7.7 Inspect `%LOCALAPPDATA%\Huddle\huddle.db` (e.g. via `sqlite3` or DB Browser) — the rows match the moments visible in the panel and any older ones from earlier sessions
- [x] 7.8 Confirm `huddle.db` is the only file the pipeline writes (no `moments.log`, no `.jpg`)
- [x] 7.9 Quit and re-launch — the panel reloads the same moments from the store, newest first, top 20
