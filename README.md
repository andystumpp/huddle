# huddle

A Windows desktop app that quietly watches your screen, sends frames to a local AI
CLI, and surfaces small observations and suggestions in a docked peek panel.

Huddle runs entirely through a **local CLI provider** — GitHub Copilot CLI, the
`claude` CLI, or Agency — using that CLI's own sign-in. There are **no API keys**:
vision and every scenario go through the one configured CLI.

## Configuration

Runtime behaviour comes from a non-secret **`huddle.config.json`**. It is optional —
with no file, Huddle uses the `claude` CLI and full-screen capture. The file is
resolved from, in order:

1. the executable's directory (next to `Huddle.exe`), then
2. `%LOCALAPPDATA%\Huddle\huddle.config.json`

It is gitignored (machine-specific) and holds **no secrets** — each CLI authenticates
through its own login.

### Options

| Key | Values | Default | Notes |
| --- | --- | --- | --- |
| `provider` | `claude` \| `copilot` \| `agency` | `claude` | Which local CLI handles vision **and** scenarios. |
| `command` | executable name/path | the provider's binary (`claude` / `copilot` / `agency`) | Override if the CLI isn't on `PATH` under its usual name. |
| `model` | model name | `claude-opus-5` | Used by Copilot/Agency. The `claude` provider picks its model per scenario and ignores this. |
| `captureDenylist` | array of strings | `[]` | Case-insensitive substrings matched against the foreground app name and window title. A match **skips the whole capture tick** — no screenshot, no CLI call, no moment. |
| `captureScope` | `fullScreen` \| `activeWindow` | `fullScreen` | `fullScreen` captures the whole primary display (rich multi-window context). `activeWindow` captures only the focused window's own pixels — nothing overlapping or behind it — which makes `captureDenylist` an exact guarantee at the cost of peripheral context. |

Every field except `provider` has a default, so naming only the provider is a complete
config.

### Examples

Minimal — run everything on Copilot with defaults:

```json
{ "provider": "copilot" }
```

Work laptop — Copilot, capture only the focused window, and never capture a few
sensitive apps:

```json
{
  "provider": "copilot",
  "captureScope": "activeWindow",
  "captureDenylist": ["1Password", "Bitwarden", "Banking"]
}
```

Default (no file) is equivalent to:

```json
{ "provider": "claude", "captureScope": "fullScreen", "captureDenylist": [] }
```

## Safeguards

- **Ephemeral screenshots.** Each captured frame is written to a temp file only for
  the CLI call and deleted immediately afterwards (success or failure). Only the text
  summary is stored — never the image.
- **Denylist.** See `captureDenylist` above. Pair it with `captureScope: activeWindow`
  to guarantee a sensitive foreground window is never sent.
