## Why

The manual snapshot button got us the full pipeline working end-to-end, but it's a designed-to-die affordance — Huddle's whole point is *ambient*. ADR 0001 specifies a 3-minute tick. We also still render seeded patterns in the Activity tab; the actual moments Huddle is observing land in a text log nobody looks at. This change closes the loop: real moments persist to SQLite, the Activity tab renders them, and a 3-minute tick fires the capture pipeline automatically. After this, the panel actually does what the product is supposed to do.

Single change because the parts only earn their keep together — storage without the tick still requires a button, the tick without storage just appends to a log file, and the UI swap without the tick still shows static data. Doing all three together makes the panel feel alive.

## What Changes

- **BREAKING:** Remove the manual "Snapshot now" camera button from the Activity tab section header (its `huddle.env` resolution and the temporary status text). Its purpose has been replaced by the tick.
- **BREAKING:** Remove `Models/Pattern.cs`, `Models/PatternSeed.cs`, and the file-log sink (`Vision/MomentLog.cs` + `%LOCALAPPDATA%\Huddle\moments.log`). The Activity tab no longer renders seeded patterns.
- Add SQLite persistence via `Microsoft.Data.Sqlite`. Store at `%LOCALAPPDATA%\Huddle\huddle.db`, single `moments` table matching ADR 0001's schema (`id`, `ts`, `app`, `window_title`, `summary`). Linear migrations applied at startup.
- Add a 3-minute tick scheduler. On app start, the scheduler fires one capture immediately, then every 180 seconds. The pause / resume button stops and restarts the tick.
- Each tick runs the existing capture → Claude vision call → store path, writing the resulting `Moment` to SQLite and prepending it to the Activity tab's observable collection.
- Activity tab renders the 20 most-recent moments from the store, newest first. The section header label changes from **"PATTERNS DETECTED N"** to **"OBSERVATIONS N"** — moments are per-tick observations, not aggregations.
- Each moment card shows: the model's 1–2 sentence summary, then a footer with the source app's monogram tile and the window title.
- The look-bar and "Watching · next look in M:SS" status now reflect the real 180-second tick, not the demo 18-second fake. Pause behaves as already specified.
- API key resolution keeps the existing four-step fallback (env → User registry → `huddle.env` next to exe → `huddle.env` at `%LOCALAPPDATA%\Huddle\`).

## Capabilities

### Modified Capabilities

- `moment-capture`: drop the manual trigger requirement and the file-log sink. Add scheduled tick and SQLite store requirements. The capture + Claude vision call requirements stay unchanged.
- `app-shell`: replace the "Activity tab content — patterns detected", "Pattern card content", and "Seeded pattern data" requirements with moment-backed equivalents. The look-bar requirement gets a concrete cadence (180 s).

## Impact

- Add `src/Huddle.App/Storage/Database.cs` — opens / creates `huddle.db`, runs migrations.
- Add `src/Huddle.App/Storage/Migrations/001_init.sql` — creates the `moments` table + `idx_moments_ts` index.
- Add `src/Huddle.App/Storage/MomentStore.cs` — `AddAsync(Moment)`, `RecentAsync(int limit)`, `CountAsync()`.
- Add `src/Huddle.App/Vision/TickScheduler.cs` — `DispatcherTimer`-based, with `Pause` / `Resume`, `SecondsRemaining`, and a `Tick` event the panel subscribes to.
- Modify `src/Huddle.App/Views/PeekPanelWindow.xaml(.cs)` — remove the snapshot button + status text + `OnSnapshotClick`; bind the Activity tab to an `ObservableCollection<Moment>` populated on load from `MomentStore.RecentAsync(20)`; subscribe to `TickScheduler.Tick` and prepend new moments as they arrive; rename section header to "OBSERVATIONS N"; drive look-bar + status from `TickScheduler.SecondsRemaining`.
- Rename `src/Huddle.App/Controls/PatternCard.xaml(.cs)` → `Controls/MomentCard.xaml(.cs)`; change its dependency property from `Pattern` to `Moment`; simplify content to summary + (app tile + window title).
- Delete `src/Huddle.App/Models/Pattern.cs`, `src/Huddle.App/Models/PatternSeed.cs`, `src/Huddle.App/Vision/MomentLog.cs`.
- Modify `src/Huddle.App/Huddle.App.csproj` — add `<PackageReference Include="Microsoft.Data.Sqlite" Version="*" />`.

## Cost note

At 3-minute cadence with Sonnet 4.6 (~$0.006 per call), continuous use is ~$0.12/hour, ~$1/day at 8 active hours, ~$25/month at the same. The pause button is the user's lever; no other guardrails added in this change.
