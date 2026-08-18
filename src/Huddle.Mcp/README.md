# Huddle.Mcp

A **local, read-only [MCP](https://modelcontextprotocol.io) server** over Huddle's SQLite
database (`%LOCALAPPDATA%\Huddle\huddle.db`). It lets Claude (Claude Desktop / Claude Code)
query your collected work history so you can draft blog / LinkedIn content grounded in it.

It opens the database **read-only** and never writes or migrates — safe to run while the
Huddle app is running (WAL mode allows a concurrent reader).

## Tools

- **`list_nudges(scenario?, sinceDays=7, limit=50)`** — curated nudges, newest first.
  Pass a scenario key to isolate one kind: `achievements` · `learnings` · `linkedin-posts`
  · `efficiency-insights`. Returns `[{ ts, scenario, title, body, sources }]`.
- **`search_moments(query, sinceDays?, limit=50)`** — raw screen observations whose summary,
  app, or window title contain the query text. Returns `[{ ts, app, windowTitle, summary }]`.
- **`get_day(date)`** — everything for one local day (`YYYY-MM-DD`): `{ moments, nudges }`.

## Build

```powershell
dotnet build Huddle.slnx -c Debug
# server exe: src\Huddle.Mcp\bin\Debug\net10.0\Huddle.Mcp.exe
```

For a stable path (survives rebuilds), publish it somewhere fixed:

```powershell
dotnet publish src\Huddle.Mcp\Huddle.Mcp.csproj -c Release -o "$env:LOCALAPPDATA\Huddle\mcp"
```

## Register the server

**Claude Code:**

```bash
claude mcp add huddle -- "C:\Users\<you>\AppData\Local\Huddle\mcp\Huddle.Mcp.exe"
```

**Claude Desktop** — add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "huddle": {
      "command": "C:\\Users\\<you>\\AppData\\Local\\Huddle\\mcp\\Huddle.Mcp.exe"
    }
  }
}
```

Then ask, e.g., *"Use huddle: pull this week's `learnings` and draft a LinkedIn post."*

## Privacy

Moments are literal screen observations. Querying them into a conversation sends that text
to Claude as normal context. Prefer the already-curated `list_nudges` over dumping raw
`search_moments` unless you need the grounding. The server itself is read-only and local.
