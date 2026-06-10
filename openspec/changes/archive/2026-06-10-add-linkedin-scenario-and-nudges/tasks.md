## 1. Storage

- [x] 1.1 Add `src/Huddle.App/Storage/Migrations/002_nudges.sql` with the `nudges` table + `idx_nudges_ts` index per `design.md` D1 (table schema is embedded resource, picked up automatically by the existing migration runner — no csproj change needed)
- [x] 1.2 Add `src/Huddle.App/Models/Nudge.cs` — `record Nudge(string Id, DateTimeOffset Ts, string Scenario, string Title, string Body, IReadOnlyList<string> Sources)`
- [x] 1.3 Add `src/Huddle.App/Storage/NudgeStore.cs` exposing `static Task AddAsync(Nudge)`, `static Task<IReadOnlyList<Nudge>> RecentAsync(int limit)`, `static Task<int> CountAsync()`. Insert serializes `Sources` to JSON via `System.Text.Json` and stores in the `sources` column. `RecentAsync` parses the JSON back on read. Follow the same WAL-checkpoint-after-insert pattern as `MomentStore.AddAsync`

## 2. LinkedIn scenario

- [x] 2.1 Add `src/Huddle.App/Scenarios/NudgeDraft.cs` — `record NudgeDraft(bool Emit, string? Title, string? Body, IReadOnlyList<string>? Sources)`; this is the structured-output deserialization target
- [x] 2.2 Add `src/Huddle.App/Scenarios/LinkedInPostsScenario.cs` — `internal static class` with `Key = "linkedin-posts"`, `Name = "LinkedIn posts"`, `Cadence = TimeSpan.FromHours(1)`, `TrailSize = 20`; private `s_lastRun = DateTimeOffset.MinValue`
- [x] 2.3 `LinkedInPostsScenario.IsDue(DateTimeOffset now) => now - s_lastRun >= Cadence`
- [x] 2.4 Define the system prompt as a `const string SystemPrompt` matching `design.md` D5 verbatim (raw-string literal)
- [x] 2.5 Define the JSON-schema dictionary as a static helper that returns the schema in `design.md` D6 for the SDK's `OutputConfig.Format = new JsonOutputFormat { Schema = ... }`
- [x] 2.6 `LinkedInPostsScenario.RunAsync(IReadOnlyList<Moment> trail)`:
  - Build user content text per `design.md` D7 (recent-moments block including the moment IDs as `m_<id>: <summary>` style, then the closing instruction)
  - Call `client.Messages.Create(...)` with `Model.ClaudeSonnet4_6`, `MaxTokens = 600`, `System = SystemPrompt`, `OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = ... } }`, one user `MessageParam` with the text block
  - Pull the first `TextBlock` from the response, deserialize as `NudgeDraft` via `System.Text.Json`
  - Always update `s_lastRun = DateTimeOffset.UtcNow` (even on `emit: false` or on throw — that's a deliberate part of the throttle)
  - If `Emit == false` or required fields are missing, return `null`
  - Otherwise return `new Nudge(UlidGenerator.Generate(), DateTimeOffset.UtcNow, Key, draft.Title!, draft.Body!, draft.Sources ?? Array.Empty<string>())`
- [x] 2.7 Wrap the whole `RunAsync` in try/catch; on exception log via `Debug.WriteLine("[Huddle] LinkedIn scenario failed: ...")` and return `null`

## 3. Wire into the panel

- [x] 3.1 Add field `private readonly ObservableCollection<Nudge> _nudges = new();` and `private const int MaxVisibleNudges = 20;` to `PeekPanelWindow`
- [x] 3.2 In `OnContentLoaded`, after the moments load completes, also `var recentNudges = await NudgeStore.RecentAsync(MaxVisibleNudges); foreach (var n in recentNudges) _nudges.Add(n); UpdateNudgesSurface();`
- [x] 3.3 In `OnSchedulerTick`, after the moment is persisted and inserted into `_moments`, add the block from `design.md` D8 — `IsDue` check, `RecentAsync` trail pull, `RunAsync`, `NudgeStore.AddAsync`, `_nudges.Insert(0, ...)`, trim, `UpdateNudgesSurface()`
- [x] 3.4 Add `UpdateNudgesSurface()` method that sets `NudgesEmptyState.Visibility` and `NudgesScroll.Visibility` based on `_nudges.Count > 0`, and updates `CountNudges.Text = _nudges.Count.ToString()`
- [x] 3.5 Call `UpdateNudgesSurface()` after the load step in 3.2 and after each insert in 3.3

## 4. UI surface

- [x] 4.1 Add `src/Huddle.App/Controls/NudgeCard.xaml(.cs)` per `design.md` D9 — `Nudge` dependency property; layout: small scenario tag row (colored dot + uppercase letter-spaced scenario name in T3), title TextBlock (SemiBold, FontSize 14.5, T1), body TextBlock (FontSize 12.5, LineHeight 20, T2, TextWrapping Wrap)
- [x] 4.2 In `PeekPanelWindow.xaml`, restructure the `NudgesContent` Grid: keep the existing empty state as `NudgesEmptyState` (rename if needed); add a sibling `ScrollViewer x:Name="NudgesScroll"` containing an `ItemsRepeater x:Name="NudgesRepeater"` whose `ItemTemplate` instantiates `controls:NudgeCard Nudge="{Binding}"`. Default visibility on the ScrollViewer is `Collapsed`
- [x] 4.3 In `OnContentLoaded`, bind `NudgesRepeater.ItemsSource = _nudges`

## 4b. Manual trigger

- [x] 4b.1 In `PeekPanelWindow.xaml`, wrap the existing `NudgesContent` in a 2-row Grid; the top row is a section header (`⊕ NUDGES N` + status text + play button) modelled on the Activity tab's section header; the bottom row contains the existing empty state + ScrollViewer
- [x] 4b.2 Add `Button x:Name="RunNowBtn"` (uses `HeaderButton` style, play-triangle Path glyph) wired to `OnRunScenariosNowClick`
- [x] 4b.3 Add `TextBlock x:Name="RunNowStatusText"` next to the button for inline status
- [x] 4b.4 `OnRunScenariosNowClick`: disable button + dim, call `MomentStore.RecentAsync(LinkedInPostsScenario.TrailSize)`, call `LinkedInPostsScenario.RunAsync(trail)`, persist + prepend the nudge if any, update surface, set status text. Re-enable button in finally

## 5. Verification

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean (0 warnings, 0 errors)
- [x] 5.2 Launch on a fresh database (`del %LOCALAPPDATA%\Huddle\huddle.db*`) — migration `002_nudges.sql` runs; the `nudges` table + `idx_nudges_ts` index exist (verify via Python sqlite3)
- [x] 5.3 Launch with an existing `huddle.db` that has only the `moments` table — migration runs, no existing data is lost
- [x] 5.4 The Nudges tab still shows the empty state when no nudges have been emitted
- [x] 5.5 Wait until the first tick fires the LinkedIn scenario (within ~3 min of startup). One of two outcomes:
  - Scenario emits — Nudges tab swaps to render the card; row visible in `nudges` table
  - Scenario stays silent — Nudges tab keeps the empty state; no row in `nudges`
- [x] 5.6 If the scenario emitted, inspect the row: `title` reads as a hook, `body` reads as 2-3 substantive sentences in principal-architect voice, `sources` is a JSON array of moment IDs that actually exist in the `moments` table
- [x] 5.7 Throttle works: after the scenario fires once, subsequent ticks within an hour do not re-fire it (verify via `Debug.WriteLine` log or by checking that no new `nudges` rows appear)
- [x] 5.8 No `.jpg` / `.png` files appear anywhere under `%LOCALAPPDATA%\Huddle\` — frames remain unstored
