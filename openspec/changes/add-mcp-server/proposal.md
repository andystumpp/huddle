## Why

Huddle has accumulated months of curated nudges (achievements, learnings, LinkedIn drafts, efficiency insights) and raw moments — but they only live inside the peek panel. To turn them into blog / LinkedIn content, the user wants to query their own work history from the Claude tools they already write in (Claude Desktop, Claude Code) and have Claude draft from it. A read-only MCP server makes the Huddle database reachable there — "draft this week's LinkedIn post from my learnings," and Claude pulls the right nudges. This is the first "content pipeline" iteration after the day-grouped review shipped.

## What Changes

- **New `Huddle.Core` class library** — the `Nudge`/`Moment` models and the storage layer (`Database`, `NudgeStore`, `MomentStore`, migrations) move out of `Huddle.App` into `Huddle.Core`, referenced by both the app and the new server. Pure refactor; app behavior unchanged.
- **New `Huddle.Mcp` project** — a local **stdio MCP server** (official C# `ModelContextProtocol` SDK) exposing read-only tools over `huddle.db`:
  - `list_nudges(scenario?, sinceDays?, limit?)` — curated nudges, filterable by **scenario key** so LinkedIn or Achievements can be pulled on their own.
  - `search_moments(query, sinceDays?, limit?)` — raw activity matching a text query.
  - `get_day(date)` — moments + nudges for one local day (the digest primitive).
- **New read-only queries in `Huddle.Core`** — moment text search and day-range reads for both stores; reuse the existing `NudgeStore.SinceAsync`.
- **Config docs** — how to register the server in Claude Code and Claude Desktop.
- Opens `huddle.db` in `Mode=ReadOnly`; never writes, never runs migrations (the app still owns those). WAL mode allows reading while the app runs.

## Capabilities

### New Capabilities
- `mcp-server`: A local, read-only MCP stdio server exposing Huddle's nudges and moments (scenario- and date-filterable, plus text search and per-day reads) to Claude Desktop / Claude Code.

### Modified Capabilities
<!-- None. The Huddle.Core extraction is an internal refactor with no requirement change to existing capabilities. -->

## Impact

- **New projects**: `src/Huddle.Core/` (class library), `src/Huddle.Mcp/` (console stdio server, `ModelContextProtocol` NuGet). Both added to `Huddle.slnx`.
- **Moved code**: `Models/{Nudge,Moment}.cs` and `Storage/{Database,MomentStore,NudgeStore}.cs` + `Storage/Migrations/*.sql` → `Huddle.Core` (namespaces stay `Huddle.Models` / `Huddle.Storage`; `Huddle.App` references the library).
- **New code**: `Huddle.Core` query methods (moment search, day-range); `Huddle.Mcp` server host + tool class.
- **Unchanged**: the WinUI app's runtime behavior, scenarios, backends, DB schema.
- **External dependency**: `ModelContextProtocol` NuGet (server side only).
- **Non-goals**: no web/remote transport or auth (local stdio only); no write tools; `LIKE` search now (FTS5 later).
- **Privacy**: moments are screen observations — querying them into a conversation sends them to Claude as normal context; the design favors curated `list_nudges` over dumping raw moments.
