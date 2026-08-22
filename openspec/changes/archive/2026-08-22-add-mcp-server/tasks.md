## 1. Extract Huddle.Core

- [x] 1.1 Create `src/Huddle.Core/Huddle.Core.csproj` (class library, `net10.0`, `Nullable`/`ImplicitUsings` enabled, `Microsoft.Data.Sqlite` package, embed `Storage/Migrations/*.sql`). Add it to `Huddle.slnx`.
- [x] 1.2 Move `Models/{Nudge,Moment}.cs` and `Storage/{Database,MomentStore,NudgeStore}.cs` + `Storage/Migrations/*.sql` from `Huddle.App` into `Huddle.Core`, keeping namespaces (`Huddle.Models`, `Huddle.Storage`).
- [x] 1.3 Add a project reference from `Huddle.App` to `Huddle.Core`; remove the moved files/migration embed from `Huddle.App.csproj`.
- [x] 1.4 `dotnet build Huddle.slnx -c Debug` is clean and the app still runs (pure refactor, no behavior change).

## 2. Read-only query methods in Huddle.Core

- [x] 2.1 `MomentStore.SearchAsync(string query, DateTimeOffset? cutoff, int limit)` — `WHERE (summary LIKE %q% OR app LIKE %q% OR window_title LIKE %q%) [AND ts >= cutoff] ORDER BY ts DESC LIMIT`.
- [x] 2.2 `MomentStore.BetweenAsync(DateTimeOffset startUtc, DateTimeOffset endUtc)` and `NudgeStore.BetweenAsync(...)` — `WHERE ts >= start AND ts < end ORDER BY ts DESC` (for `get_day`).
- [x] 2.3 `NudgeStore.SinceByScenarioAsync(string? scenario, DateTimeOffset cutoff, int limit)` (or reuse `SinceAsync` + filter) so `list_nudges` can isolate a scenario and cap count.
- [x] 2.4 Ensure all new store methods open connections read-safe and reuse the existing `Read`/row-mapping helpers.

## 3. Huddle.Mcp server

- [x] 3.1 Create `src/Huddle.Mcp/Huddle.Mcp.csproj` (console app, `net10.0`, `ModelContextProtocol` + `Microsoft.Extensions.Hosting` packages), reference `Huddle.Core`, add to `Huddle.slnx`.
- [x] 3.2 `Program.cs`: `Host.CreateApplicationBuilder(args)` → `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` → `RunAsync()`. Log to stderr only (stdout is the MCP channel).
- [x] 3.3 A small `DbPath` helper resolving `%LOCALAPPDATA%\Huddle\huddle.db`; stores open it `Mode=ReadOnly` (add a read-only open path in `Huddle.Core` or pass a read-only connection string).
- [x] 3.4 `[McpServerToolType]` tool class with `[McpServerTool]` methods:
  - `list_nudges(string? scenario, int sinceDays = 7, int limit = 50)` → JSON `[{ts,scenario,title,body,sources}]`.
  - `search_moments(string query, int? sinceDays, int limit = 50)` → JSON `[{ts,app,windowTitle,summary}]`.
  - `get_day(string date)` → JSON `{moments:[…], nudges:[…]}` for the local day.
  - Each has a clear `[Description]`; return compact JSON via System.Text.Json.

## 4. Config docs

- [x] 4.1 Document registration: `claude mcp add huddle -- <path>\Huddle.Mcp.exe` (Claude Code) and a `claude_desktop_config.json` block (Claude Desktop). Add to the repo (e.g. `src/Huddle.Mcp/README.md`).

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean; the WinUI app still launches and captures/emits as before.
- [x] 5.2 Run the server manually and exercise the tools (MCP inspector, or a scripted stdio `initialize` + `tools/call`), confirming: `list_nudges scenario=linkedin-posts` returns only LinkedIn nudges; `list_nudges scenario=achievements` returns only achievements; `search_moments` matches on summary text; `get_day` returns a day's moments + nudges. Confirm read-only (server holds no write lock while the app runs).
- [x] 5.3 Register in one real client (Claude Code or Desktop) and confirm the tools are discovered and callable end-to-end.
- [x] 5.4 Record commands and outcomes in tasks.md §Verification.

## Verification

**Build** — `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)` across all three projects (App, Core, Mcp).

**Refactor is behavior-preserving** — App + Core build; the WinUI app relaunches and runs after `Models`/`Storage` moved to `Huddle.Core` (verified live).

**MCP server (scripted stdio JSON-RPC session, real DB, read-only):**
- `tools/list` → `get_day`, `search_moments`, `list_nudges`.
- `list_nudges scenario=achievements` → only achievements; `list_nudges scenario=linkedin-posts` → only linkedin-posts (both scenario isolations confirmed).
- `search_moments query="LinkedIn"` → matching moments (summary/app/title LIKE).
- `get_day date=2026-08-15` → 25 moments + 4 nudges for that local day.
- Every tool call returned `IsError = False`; the server opened `huddle.db` `Mode=ReadOnly`.
- (A real client keeps stdin open; my harness needed a stdin-hold so the async response flushed — a test artifact, not a server issue.)

**Client registration** — the protocol path (`initialize` + `tools/list` + `tools/call`) is exactly what Claude Desktop / Claude Code drive, so it is client-ready; the exact `claude mcp add` command and `claude_desktop_config.json` block are documented in `src/Huddle.Mcp/README.md`. Registered live in both Claude Code (`~/.claude.json`) and Claude Desktop (`claude_desktop_config.json`) and confirmed working after a full Desktop restart.

**`get_day` local-today fix (found in client use)** — the client model's "today" is UTC-based and was a day ahead of the user's local day (machine at UTC−08:00), so `get_day("<utc-today>")` came back empty and the model fell back a day. Fixed: `date` is now optional and defaults to the server's local day, with the tool description steering the model to omit it for "today". Verified: `get_day()` (no date) resolves to the local day and returns that day's moments + nudges.
