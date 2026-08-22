# mcp-server Specification

## Purpose

A local, read-only Model Context Protocol (MCP) stdio server over Huddle's SQLite database. It lets an MCP client — Claude Desktop or Claude Code — query the user's collected moments and nudges. The server opens the database read-only and never writes to it, so it can run alongside the Huddle app, which owns writes and migrations.

## Requirements

### Requirement: Local read-only MCP stdio server

The system SHALL provide a standalone MCP server that communicates over stdio and exposes read-only access to the Huddle database at `%LOCALAPPDATA%\Huddle\huddle.db`. The server SHALL open the database read-only and SHALL NOT modify it or run migrations, so it can run alongside the Huddle app (which owns writes and migrations). It SHALL advertise its tools via the MCP protocol so a client such as Claude Desktop or Claude Code can discover and call them.

#### Scenario: The server runs alongside the app

- **WHEN** the MCP server starts while the Huddle app is running
- **THEN** it opens `huddle.db` read-only and answers tool calls without blocking or corrupting the app's writes

#### Scenario: Tools are discoverable

- **WHEN** a client connects and lists tools
- **THEN** the server returns `list_nudges`, `search_moments`, and `get_day`

### Requirement: Query nudges by scenario and date

The `list_nudges` tool SHALL return nudges as structured records (`ts`, `scenario`, `title`, `body`, `sources`), newest first, within a date window (default the last 7 days) and optionally limited in count. It SHALL accept an optional scenario key so a single scenario can be retrieved on its own.

#### Scenario: Retrieve one scenario in isolation

- **WHEN** `list_nudges` is called with scenario `linkedin-posts`
- **THEN** only LinkedIn-posts nudges are returned, newest first

#### Scenario: Retrieve all recent nudges

- **WHEN** `list_nudges` is called without a scenario
- **THEN** nudges from every scenario within the date window are returned, newest first

### Requirement: Search moments by text

The `search_moments` tool SHALL return moments (`ts`, `app`, `windowTitle`, `summary`) whose summary, app, or window title match a supplied text query, within an optional date window and count limit, newest first.

#### Scenario: Find moments about a topic

- **WHEN** `search_moments` is called with a query string
- **THEN** moments whose summary, app, or window title contain that text are returned, newest first

### Requirement: Read a single day

The `get_day` tool SHALL return the moments and nudges emitted on one local calendar day. The date parameter is optional; when omitted, the tool SHALL use the server machine's current local day, so a "today" request resolves against local time rather than the client's UTC-based notion of today. The response SHALL report the resolved local date it returned.

#### Scenario: Retrieve a day's activity

- **WHEN** `get_day` is called with a date
- **THEN** the moments and nudges whose timestamps fall within that local day are returned, grouped as moments and nudges

#### Scenario: Default to the local today

- **WHEN** `get_day` is called with no date
- **THEN** it returns the current local day's moments and nudges, and reports that resolved date
