## Context

Huddle's data lives in SQLite at `%LOCALAPPDATA%\Huddle\huddle.db` (`moments` and `nudges` tables), accessed today only by the WinUI app via `Huddle.App/Storage` (`Database`, `MomentStore`, `NudgeStore`) over `Huddle.App/Models` (`Nudge`, `Moment`). To draft content, the user wants to query this history from **Claude Desktop / Claude Code** — which speak MCP over a locally-spawned stdio process.

Two Claude clients will read the same database the app writes. SQLite WAL mode allows a concurrent reader, so a read-only server is safe alongside the running app.

## Goals / Non-Goals

**Goals:**
- Expose nudges + moments to local Claude clients, read-only, with scenario/date filtering and text search.
- Reuse the existing models and storage — extract `Huddle.Core` so app and server share one implementation.
- Keep the surface small and composable (three tools).

**Non-Goals:**
- Web / remote (HTTP) transport or auth — local stdio only this iteration.
- Any write tool — the server never mutates the DB or runs migrations.
- FTS5 search (a `LIKE` scan is enough for personal volume now).
- Changing the app's runtime behavior (the extraction is a pure move).

## Sequence

A Claude client spawns the server, discovers its tools, and calls them; each call is a read-only query against `huddle.db` through `Huddle.Core`.

```mermaid
sequenceDiagram
    participant Claude as Claude (Desktop / Code)
    participant Mcp as Huddle.Mcp (stdio host)
    participant Core as Huddle.Core (read-only stores)
    participant DB as huddle.db (Mode=ReadOnly)

    rect rgb(245,245,245)
    Note over Claude,Mcp: 1. Spawn + handshake
    Claude->>Mcp: launch process; initialize; tools/list
    Mcp-->>Claude: list_nudges · search_moments · get_day
    end

    rect rgb(245,245,245)
    Note over Claude,DB: 2. Tool call → query
    Claude->>Mcp: tools/call list_nudges { scenario:"learnings", sinceDays:7 }
    Mcp->>Core: NudgeStore.SinceAsync(cutoff) [+ scenario/limit filter]
    Core->>DB: SELECT … WHERE ts>=? [AND scenario=?] ORDER BY ts DESC
    DB-->>Core: rows → List<Nudge>
    Core-->>Mcp: List<Nudge>
    Mcp-->>Claude: JSON [ {ts,scenario,title,body,sources}, … ]
    end

    rect rgb(245,245,245)
    Note over Claude: 3. Compose
    Claude->>Claude: draft blog / LinkedIn post from the returned records
    end
```

### 1. Spawn + handshake

**Contract** — In: the client's configured launch command (`Huddle.Mcp.exe`). Out: an MCP session over stdio advertising the three tools with their JSON schemas. No arguments, no auth.

**How** — `Huddle.Mcp` is a console app built on the official `ModelContextProtocol` SDK: `Host.CreateApplicationBuilder` → `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` → `RunAsync()`. Tools are `[McpServerTool]` methods on an `[McpServerToolType]` class, each with a `[Description]`; the SDK generates the schema from the method signature.

### 2. Tool call → query (the tool surface)

**Contract** — three read-only tools; all return newest-first JSON.

- **`list_nudges(scenario?, sinceDays?=7, limit?)`** → `[{ ts, scenario, title, body, sources[] }]`. `scenario` is a scenario key (`achievements` | `learnings` | `linkedin-posts` | `efficiency-insights`); omitting it returns all. Backed by `Huddle.Core.NudgeStore` — reuse `SinceAsync(cutoff)` and filter by scenario/limit (or a `SinceByScenarioAsync(scenario, cutoff, limit)` overload).
- **`search_moments(query, sinceDays?, limit?)`** → `[{ ts, app, windowTitle, summary }]`. Backed by a new `MomentStore.SearchAsync(query, cutoff, limit)` — `WHERE (summary LIKE %q% OR app LIKE %q% OR window_title LIKE %q%) [AND ts>=cutoff] ORDER BY ts DESC LIMIT`.
- **`get_day(date?)`** → `{ date, moments:[…], nudges:[…] }`. `date` is a local calendar day (`YYYY-MM-DD`), **optional** — omitted means the server's current **local** day. The tool converts to a `[startUtc, endUtc)` window and calls new `MomentStore.BetweenAsync` / `NudgeStore.BetweenAsync(startUtc, endUtc)`, and echoes back the resolved local `date`. Defaulting to local today matters because the client model's "today" is UTC-based and can be a day ahead of the user's local day (found in client testing) — omitting the date lets the server, which knows the real local date, resolve it.

**How** — Each tool method resolves the DB path (`%LOCALAPPDATA%\Huddle\huddle.db`), calls the matching `Huddle.Core` store method, and serializes the records to JSON (System.Text.Json). `sinceDays` is turned into a UTC cutoff (`DateTimeOffset.UtcNow - days`). `get_day` computes local-day bounds and converts to UTC so the `ts` (stored ISO-8601 UTC) comparison is correct.

### 3. Compose

**Contract** — In: the returned records (in the Claude conversation). Out: a drafted post. Purely the model's job; the server's responsibility ends at returning data.

**How** — The user prompts Claude ("draft a LinkedIn post from these learnings," optionally pasting a couple of their own posts as style examples). Curated `list_nudges` output is the preferred input; `search_moments` is for grounding a specific claim.

## Decisions

### D1: Extract `Huddle.Core` now (shared models + storage)

Move `Models/{Nudge,Moment}.cs` and `Storage/{Database,MomentStore,NudgeStore}.cs` + `Migrations/*.sql` into a new `Huddle.Core` library; `Huddle.App` and `Huddle.Mcp` both reference it. Namespaces stay (`Huddle.Models`, `Huddle.Storage`) so the app's code is untouched beyond the project reference. **Alternative:** duplicate read-only queries in the server — rejected; the models and DB access are exactly what both sides need, so a shared library prevents drift (the user chose this).

### D2: Read-only, no migrations

The server opens `Mode=ReadOnly` and never calls `Database.InitializeAsync`. The app remains the sole writer/migrator. This keeps concurrent access safe (WAL reader) and the server incapable of corrupting data.

### D3: `Huddle.Core` stays UI-agnostic

Only models + storage move — no WinUI types. `Huddle.Core` targets a plain library TFM (e.g. `net10.0`) with `Microsoft.Data.Sqlite`, so the console server can reference it without the Windows App SDK.

### D4: Three tools, JSON out

`list_nudges` / `search_moments` / `get_day` cover "curated content," "raw grounding," and "one day." `list_scenarios` (metadata) is deferred until a client needs it. JSON (not prose) is returned so the model can slice it; the user's prompt does the shaping.

## Risks / Trade-offs

- **[Sensitive data enters the conversation]** → moments are screen observations; querying them sends them to Claude as context. Mitigated by favoring curated `list_nudges` and documenting the trade-off; read-only server means no new write surface.
- **[`Huddle.Core` TFM vs the app's Windows TFM]** → the app targets `net10.0-windows…`; the library targets `net10.0`. A Windows-target app can reference a `net10.0` library fine; verify the build during apply.
- **[`ModelContextProtocol` is pre-1.0]** → API may shift; pin the version and keep the tool class thin so an SDK bump is contained.
- **[`LIKE` search is coarse]** → acceptable at personal volume; FTS5 is a later upgrade with no tool-surface change.

## Migration Plan

No data migration. Ship the two new projects; the app is unaffected. The user registers the server per-client (`claude mcp add huddle -- <path>\Huddle.Mcp.exe`, or a `claude_desktop_config.json` block). Rollback is removing the registration; the DB and app are untouched.

## Open Questions

- Exact packaging of `Huddle.Mcp` for the config command — framework-dependent exe vs a published self-contained exe; pick during apply based on what launches cleanly from the client config.
