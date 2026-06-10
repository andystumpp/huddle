## Context

We have a working capture + Claude pipeline triggered by a button, and an Activity tab that renders four hand-written seed patterns. ADR 0001 specifies the actual product shape: a 3-minute tick that captures + summarizes the screen, writes to SQLite, and surfaces in the panel. This change wires those three pieces — store, scheduler, panel binding — into one coherent slice.

The seeded patterns were always scaffolding. So was the manual button. So was the moments.log file. All three retire together.

## Goals / Non-Goals

**Goals:**

- One SQLite database at `%LOCALAPPDATA%\Huddle\huddle.db`, single `moments` table, schema matching ADR 0001 exactly.
- A tick scheduler that fires the capture pipeline immediately on startup, then every 180 seconds.
- Pause button stops the tick and freezes the look-bar; resume restarts both (the next tick fires a full 180 s later).
- Activity tab is backed by an `ObservableCollection<Moment>` populated on load and updated on each new moment.
- Newest moment at the top; cap the rendered list at 20.
- Section header reads **"OBSERVATIONS N"** and the count tracks the rendered collection.
- Each moment card shows the summary prominently and one source-app tile + window title in a footer row.
- Look-bar and "Watching · next look in M:SS" status reflect the real 180 s tick.
- The capture pipeline keeps the existing API-key resolution (env → User → `huddle.env`) and Win32 BitBlt + Sonnet 4.6 path — no change to that surface.

**Non-Goals:**

- **No system-idle detection.** ADR 0001 calls for "paused when system idle" — accepted scope deferral. Tick runs while panel is open regardless of input idle. Manual pause is the user's lever.
- **No retry / backoff** beyond the SDK's default `max_retries`.
- **No scenario calls / nudges** — Activity is moments only; Nudges tab stays empty.
- **No moment dismissal or edit.** Moments are immutable observations; the store is append-only at the API level (deletion would require new code we don't write).
- **No per-day budget cap, no cost guard.** Pause is the only knob; we flag the cost note in the proposal and move on.
- **No background capture when the panel is closed.** Closing the window still exits the process (per existing `app-shell` lifecycle). Tray-only background mode is a later change.
- **No streaming, no prompt caching** on the Claude call. Single-shot per tick is enough.
- **No multi-monitor support** — primary display only, same as before.

## Decisions

### D1. `Microsoft.Data.Sqlite`, single db file, linear migrations

- **Choice:** `<PackageReference Include="Microsoft.Data.Sqlite" Version="*" />`. Open the connection with `Pooling=True`, `Cache=Shared`. WAL mode and `synchronous=NORMAL` set via `PRAGMA` after connection open.
- **Path:** `%LOCALAPPDATA%\Huddle\huddle.db` — same parent as the existing `startup-error.log` / former `moments.log`.
- **Migrations:** a tiny static `Database.ApplyMigrationsAsync` runs every script in `Storage/Migrations/*.sql` in lexical order against an `__migrations` bookkeeping table. For this change, only `001_init.sql` exists.
- **Schema (ADR 0001 verbatim):**

  ```sql
  CREATE TABLE IF NOT EXISTS moments (
      id           TEXT PRIMARY KEY,
      ts           TEXT NOT NULL,           -- ISO-8601 UTC
      app          TEXT NOT NULL,
      window_title TEXT NOT NULL,
      summary      TEXT NOT NULL
  );
  CREATE INDEX IF NOT EXISTS idx_moments_ts ON moments(ts DESC);
  ```

- **Rationale:** ADR 0001 named SQLite; `Microsoft.Data.Sqlite` is the lightest official option. Tiny migration table beats an EF Core or Dapper dependency.
- **Alternative:** `sqlite-net-pcl` — friendlier ORM, but pulls weight for two queries.

### D2. `MomentStore` API surface

- **Choice:** Three methods.
  - `Task AddAsync(Moment moment)` — inserts. Throws on PK collision (a UUID/ULID dupe should never happen, but we don't silently swallow it).
  - `Task<IReadOnlyList<Moment>> RecentAsync(int limit)` — `ORDER BY ts DESC LIMIT @limit`.
  - `Task<int> CountAsync()` — for the section header. Cheap.
- **Rationale:** Smallest surface that the panel needs. We can grow it (filter by app, time range, etc.) when something asks.
- **Threading:** all methods are async over `await connection.OpenAsync()` / `command.ExecuteReaderAsync()`. We open + dispose a connection per call; connection pooling keeps this cheap. The store does not maintain a long-lived connection.

### D3. `TickScheduler` is a thin wrapper over `DispatcherTimer`

- **Choice:** `Vision/TickScheduler.cs`. Holds a `DispatcherTimer` (`Interval = TimeSpan.FromSeconds(1)`), exposes:
  - `int SecondsRemaining { get; }` — decrements per second.
  - `bool IsPaused { get; }`.
  - `event Action Tick` — raised when `SecondsRemaining` rolls from 1 to 0. After firing, `SecondsRemaining` resets to 180.
  - `void Start()` — fires an immediate `Tick`, then begins counting down from 180.
  - `void Pause()` / `void Resume()` — flip `IsPaused`. Pause leaves the timer running but stops the countdown; resume snaps `SecondsRemaining` back to 180 and counts down again.
- **Rationale:** Single source of truth for tick. The panel binds the look-bar + status to `SecondsRemaining` and subscribes to `Tick`. No duplicate timers.
- **Pause semantics:** resume snaps to a full 180 s — simplest UX, no "remembered remainder". If the user pauses for a long time and resumes, they get the same fresh 180-second cycle as on startup. Matches the existing look-bar behavior.

### D4. Capture pipeline orchestration moves into the panel

- **Choice:** The `OnTick` handler in `PeekPanelWindow.xaml.cs` does (in order):
  1. `ForegroundContext.Read()`
  2. `ScreenCapture.CaptureAsJpegAsync()`
  3. `MomentExtractor.ExtractAsync(jpeg, foreground)`
  4. Construct a `Moment` (new ULID, `DateTimeOffset.UtcNow`, etc.)
  5. `MomentStore.AddAsync(moment)`
  6. `_moments.Insert(0, moment)` (the bound `ObservableCollection`). If `_moments.Count > 20`, trim from the tail.
  7. Update `ObservationCountText`.
- **No re-architecture into a service.** The panel is one window; injecting an interface is YAGNI per `CLAUDE.md`. When the capture loop gets a second consumer (the scenario layer), we'll extract it then.
- **Errors:** catch `Exception` at the orchestration level; log a single line to `Debug.WriteLine` so it shows in attached debuggers; do not surface in UI. The tick keeps cycling.

### D5. Moment card replaces pattern card, simpler content

- **Choice:** `Controls/PatternCard.xaml(.cs)` → `Controls/MomentCard.xaml(.cs)`. Dependency property `Moment` (typed `Models.Moment`).
- **Content (top to bottom):**
  - Body text: `Moment.Summary`, primary text color, `FontSize="13"`, `LineHeight="20"`, `TextWrapping="Wrap"`.
  - Footer row: `AppTile` (size 22, monogram for `Moment.App`) + window title (`FontSize="11"`, `T3` color, `TextTrimming="CharacterEllipsis"`, no wrap).
- **No title, no scenario rail, no badge.** The summary is the content.
- **`AppTile`:** unchanged — keep the existing monogram + tint table. The `app` field from a moment uses the same `Code.exe` / `Chrome` / `Slack` / `Windows Terminal` keys the prototype defined, so existing tile data covers our common cases. Unknown apps (e.g. `Cursor.exe`) fall through to the existing "?" fallback.
- **Window title** is displayed full but trimmed at the card edge — gives a useful "what was open" without taking another line.

### D6. Activity tab data flow

- **On `OnContentLoaded`:**
  1. Initialize the SQLite database (idempotent — runs migrations if needed).
  2. Construct `_moments = new ObservableCollection<Moment>()` and bind it to `MomentsRepeater.ItemsSource`.
  3. `var recent = await MomentStore.RecentAsync(20); foreach (m in recent) _moments.Add(m);`
  4. Update the section header count.
  5. Start the `TickScheduler`. (Step 5 fires an immediate `Tick`, which kicks off the first capture in the background.)
- **On `Tick`:** see D4.
- **On `Pause`:** call `TickScheduler.Pause()`. In-flight captures complete normally; only future ticks are suppressed.

### D7. Section header rename: "PATTERNS DETECTED" → "OBSERVATIONS"

- **Choice:** Rename the section header label. Moments are per-tick observations, not aggregations; calling them "patterns" overstates what's there.
- **Trade-off:** UI text churn. Worth it because the wrong word becomes load-bearing fast.

### D8. Remove the manual snapshot button + status text + `OnSnapshotClick`

- **Choice:** Delete the camera button XAML, the `SnapshotStatusText`, `OnSnapshotClick`, `ResolveApiKey`, `EnvFileCandidates`, `ReadKeyFromEnvFile`, `s_errorBrush`, `s_okBrush`, and `ShowStatus`.
- **Wait — we still need `ResolveApiKey`.** The tick still calls Claude. Keep the key-resolution helpers but move them into `MomentExtractor` (where they belong now that the panel isn't the one checking). The SDK still reads `ANTHROPIC_API_KEY` from process env automatically; `MomentExtractor.ExtractAsync` calls `ResolveApiKey` first to populate the process env from registry / `huddle.env` so the SDK lookup works.
- **Rationale:** The four-step fallback was useful; only its UI was throwaway.

### D9. Delete `MomentLog`

- **Choice:** Drop `Vision/MomentLog.cs` entirely. SQLite is the moments store now. Failures from the tick get a `Debug.WriteLine` and nothing more.
- **Rationale:** No second sink, no synchronization concern. If we miss surfacing a failure, we'll add a "Last error" affordance to the settings flyout when that flyout exists.

## Risks / Trade-offs

- **[3-min cadence + panel-open default = continuous API spend if forgotten]** → Mitigation: pause button works, look-bar visibly counts down so the user sees the next call coming. Cost flagged in proposal.
- **[ObservableCollection cap at 20 = visible "fall off the bottom" effect when 21st arrives]** → Accepted. No transition. The list shifts; the oldest disappears. When we grow to "show more / older", that's its own change.
- **[Moments without window-title context might read oddly]** → Accepted. The summary still describes what's on screen; an empty title is just less attribution.
- **[Sqlite `Pooling=True` keeps file handles open]** → Standard, expected. Connections are released to the pool, not closed; on app exit they're cleaned up.
- **[Tick races a slow Claude call: the next tick fires while the previous is still in flight]** → Mitigation: at 3 min, this is theoretical (calls take ~1–3 s). If it does happen, both ticks execute their pipelines in parallel and both rows insert independently — no shared state to corrupt. Add overlap-prevention only if we see it in real use.

## Open Questions

- Should the section header rename to "OBSERVATIONS" — or do you want to keep "PATTERNS DETECTED"? Decided in D7: rename. Easy to flip back.
- Should pause auto-clear after N minutes? No. Pause is sticky until the user clicks resume.
- Do we want a "Force snapshot now" affordance hidden somewhere for testing? Resisted in this change (designed-to-die replaced with designed-to-be-gone). If you want it back later, a small button in the settings flyout is the right home.
