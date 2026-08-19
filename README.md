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
| `scenarios` | object | (built-ins) | Choose the active scenario set per machine — disable built-ins and/or add your own. See [Scenarios](#scenarios) below. |

Every field except `provider` has a default, so naming only the provider is a complete
config. Configuration is read once at startup, so **restart Huddle** to pick up an edit.

### Scenarios

Scenarios are the things Huddle surfaces (Achievements, Learnings, LinkedIn posts,
Efficiency insights). The optional `scenarios` section lets a machine run a different set
— turn built-ins off and define your own — without a rebuild. Omit it and the four
built-ins run as before.

```jsonc
"scenarios": {
  "disabled": ["linkedin-posts"],   // built-in keys to turn off
  "custom": [ /* your own scenarios, see fields below */ ]
}
```

Built-in keys: `achievements`, `learnings`, `linkedin-posts`, `efficiency-insights`.

A **custom scenario** needs only `key` and `systemPrompt`; everything else defaults:

| Field | Default | Notes |
| --- | --- | --- |
| `key` | *(required)* | Unique id; must not collide with a built-in or another custom. |
| `systemPrompt` | *(required)* | The prompt — describe **when to emit, when to stay silent, and the voice**. Do not describe the output JSON; that shape is enforced for you. May be a **string, or an array of lines** joined with newlines into one prompt (see below). |
| `displayName` | `key` uppercased | The uppercase label on the nudge card. |
| `accentColorHex` | `#6BA6FF` | Nudge-card accent. |
| `cadenceHours` | `6` | How often it may run. |
| `trailSize` | `60` | How many recent moments it sees. |
| `priorNudgesSize` | `10` | How many of its own recent nudges it sees (for dedup). |
| `model` | `sonnet` | Claude alias (`opus`/`sonnet`/`haiku`); ignored by Copilot/Agency, which use the top-level `model`. |
| `effort` | *(none)* | `low`\|`medium`\|`high`\|`xhigh`\|`max` (Claude only). |
| `webSearch` | `false` | Ground the answer in a live search where the provider supports it. |

A custom entry that is invalid (missing `key`/`systemPrompt`, a colliding `key`, or an
unrecognized `model`/`effort`) is skipped; the other scenarios still run.

**Writing a long prompt.** JSON strings can't hold line breaks, so a long `systemPrompt`
can instead be an **array of lines** — each element is one line, and they are joined with
newlines into a single prompt (it is still one prompt, not several):

```jsonc
"systemPrompt": [
  "You are Huddle's Value Delivery scenario.",
  "",
  "Watching the recent trail, surface ONE concrete, higher-leverage move,",
  "or return {\"emit\": false} with a reason."
]
```

The config file may also contain `//` comments and trailing commas (as the annotated
examples here use), so you can document your own config inline.

Every field at once, for reference (only `key` and `systemPrompt` are required — delete
any other line to accept its default):

```jsonc
"scenarios": {
  "disabled": ["linkedin-posts", "efficiency-insights"],
  "custom": [
    {
      "key": "value-delivery",        // required — unique id, no collision with a built-in
      "displayName": "VALUE-DELIVERY",// uppercase label on the nudge card (default: key uppercased)
      "accentColorHex": "#6BA6FF",    // nudge-card accent
      "cadenceHours": 6,              // how often it may run
      "trailSize": 60,                // recent moments it sees
      "priorNudgesSize": 10,          // its own recent nudges it sees (dedup)
      "model": "opus",                // claude alias opus|sonnet|haiku (default sonnet); ignored by copilot/agency
      "effort": "high",               // low|medium|high|xhigh|max (claude only); omit for none
      "webSearch": false,             // ground in a live search where the provider supports it
      "systemPrompt": "required — describe when to emit, when to stay silent, and the voice"
    }
  ]
}
```

### Examples

Minimal — run everything on Copilot with defaults:

```json
{ "provider": "copilot" }
```

Work laptop — Copilot, capture only the focused window, and never capture your
confidential comms, credential prompts, or secure messaging:

```json
{
  "provider": "copilot",
  "captureScope": "activeWindow",
  "captureDenylist": ["Outlook", "Teams", "Windows Security", "1Password", "Signal", "WhatsApp"]
}
```

Each entry is a case-insensitive substring matched against **both** the foreground
app's process name and its window title, so short distinctive tokens work best:

| Entry | Catches | Why exclude |
| --- | --- | --- |
| `Outlook` | `OUTLOOK.EXE` (classic), new Outlook (`olk.exe`) and Outlook-on-the-web by title | email — customer data, HR threads, confidential mail |
| `Teams` | `ms-teams.exe` (new) and `Teams.exe` (classic), plus "… \| Microsoft Teams" titles | chats, calls, screen-shares, 1:1s |
| `Windows Security` | the OS credential / sign-in dialog | passwords, smartcard PIN, MFA prompts |
| `1Password` | your password manager (swap for whichever you use) | secrets vault |
| `Signal`, `WhatsApp` | personal / secure messengers | private conversations |

Add your org's internal tools by a distinctive title substring (an HR/payroll portal,
a security dashboard, anything showing PII or unreleased info). Avoid broad tokens like
`Mail` or `Sign in` that would over-skip. Pair the denylist with `captureScope:
activeWindow` so a sensitive window that is merely *visible behind* your active one is
also never captured — in `fullScreen` mode the denylist only skips a tick when the
sensitive app is the **focused** window.

Work laptop with a custom scenario — the above, plus turn off LinkedIn and add a
**value-delivery** coach that watches your work and surfaces one higher-leverage move:

```json
{
  "provider": "copilot",
  "captureScope": "activeWindow",
  "captureDenylist": ["Outlook", "Teams", "Windows Security", "1Password"],
  "scenarios": {
    "disabled": ["linkedin-posts"],
    "custom": [
      {
        "key": "value-delivery",
        "displayName": "VALUE",
        "accentColorHex": "#E0A458",
        "cadenceHours": 2,
        "model": "opus",
        "effort": "high",
        "systemPrompt": "PLACEHOLDER — replace with your own. Watching the recent trail, surface ONE concrete, higher-leverage way to deliver or demonstrate value in the current work (turn a one-off into reuse, make impact visible to a stakeholder, unblock others), or return {\"emit\": false} with a reason. Second-person, specific, no fluff."
      }
    ]
  }
}
```

The `systemPrompt` here is a starter — tune it in place and restart; no rebuild needed.

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
