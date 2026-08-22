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
| `model` | model name | `claude-opus-5` | The **default** model for Copilot/Agency (a scenario's own Copilot-native `model` overrides it per-scenario; see [Scenarios](#scenarios)). The `claude` provider picks its model per scenario and ignores this. |
| `captureDenylist` | array of strings | `[]` | Case-insensitive substrings matched against the foreground app name and window title. A match **skips the whole capture tick** — no screenshot, no CLI call, no moment. |
| `captureScope` | `fullScreen` \| `activeWindow` | `fullScreen` | `fullScreen` captures the whole primary display (rich multi-window context). `activeWindow` captures only the focused window's own pixels — nothing overlapping or behind it — which makes `captureDenylist` an exact guarantee at the cost of peripheral context. |
| `skipSensitiveMoments` | `true` \| `false` | `true` | When the vision model flags a frame as sensitive (compensation, health, credentials, PII), the tick stores **nothing**. Summaries never contain sensitive values regardless; this additionally drops the whole moment. Set `false` to keep the value-free summary for sensitive frames. |
| `scenarios` | object | (built-ins) | Choose the active scenario set per machine — disable built-ins and/or add your own. See [Scenarios](#scenarios) below. |

Every field except `provider` has a default, so naming only the provider is a complete
config. Configuration is read once at startup, so **restart Huddle** to pick up an edit.

### Scenarios

Scenarios are the things Huddle surfaces — Achievements, Learnings, LinkedIn posts,
Efficiency insights, or your own. They are defined **entirely** in the `scenarios` array
of `huddle.config.json`; there are no scenarios baked into the app. **With no `scenarios`
configured, Huddle produces no nudges** (it still captures moments).

The repo ships [`huddle.config.example.json`](huddle.config.example.json) with the four
default scenarios spelled out. To start, copy it to your config location and rename it to
`huddle.config.json` (e.g. `%LOCALAPPDATA%\Huddle\huddle.config.json`), then tune it — edit
a prompt, change a cadence, drop a scenario, add your own — by hand or by pointing an agent
at the file (*"read my huddle.config.json, drop linkedin, add a value-delivery scenario
tuned for internal impact reviews"*).

Each scenario in the array needs only `key` and `systemPrompt`; everything else defaults:

| Field | Default | Notes |
| --- | --- | --- |
| `key` | *(required)* | Unique id (must not repeat another scenario's key). |
| `systemPrompt` | *(required)* | The prompt — describe **when to emit, when to stay silent, and the voice**. Do not describe the output JSON; that shape is enforced for you. May be a **string, or an array of lines** joined with newlines into one prompt (see below). |
| `displayName` | `key` uppercased | The uppercase label on the nudge card. |
| `accentColorHex` | `#6BA6FF` | Nudge-card accent. |
| `cadenceHours` | `6` | How often it may run. |
| `trailSize` | `60` | How many recent moments it sees. |
| `priorNudgesSize` | `10` | How many of its own recent nudges it sees (for dedup). |
| `model` | `sonnet` | **Provider-relative.** On `claude`: an alias (`opus`/`sonnet`/`haiku`). On `copilot`/`agency`: a Copilot model name (e.g. `claude-opus-5`) is used per-scenario as-is; a *bare* Claude alias (`opus`/`sonnet`/`haiku`, incl. the default) isn't a Copilot name, so it falls back to the top-level `model`. |
| `effort` | *(none)* | `low`\|`medium`\|`high`\|`xhigh`\|`max`. Reasoning effort, applied on both Claude (`--effort`) and Copilot/Agency (`--effort`). |
| `webSearch` | `false` | Ground the answer in a live search where the provider supports it. |

A scenario that is invalid (missing `key`/`systemPrompt`, a duplicate `key`, or an
unrecognized `model`/`effort`) is skipped; the others still run.

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
"scenarios": [
  {
    "key": "value-delivery",        // required — unique id
    "displayName": "VALUE-DELIVERY",// uppercase label on the nudge card (default: key uppercased)
    "accentColorHex": "#6BA6FF",    // nudge-card accent
    "cadenceHours": 6,              // how often it may run
    "trailSize": 60,                // recent moments it sees
    "priorNudgesSize": 10,          // its own recent nudges it sees (dedup)
    "model": "opus",                // provider-relative (see the model row above)
    "effort": "high",               // low|medium|high|xhigh|max; omit for none
    "webSearch": false,             // ground in a live search where the provider supports it
    "systemPrompt": "required — describe when to emit, when to stay silent, and the voice"
  }
]
```

### Examples

Minimal provider selection — captures moments on Copilot; add a `scenarios` array (or copy
the example config) to get nudges:

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

Work laptop — copy `huddle.config.example.json`, keep the scenarios you want, and add your
own. `scenarios` is an array, so dropping LinkedIn/Efficiency is just leaving them out:

```jsonc
{
  "provider": "copilot",
  "captureScope": "activeWindow",
  "captureDenylist": ["Outlook", "Teams", "Windows Security", "1Password"],
  "scenarios": [
    // … keep achievements + learnings from the example, then add: …
    {
      "key": "value-delivery",
      "displayName": "VALUE",
      "accentColorHex": "#E0A458",
      "cadenceHours": 2,
      "model": "opus",
      "effort": "high",
      "systemPrompt": [
        "PLACEHOLDER — replace with your own.",
        "Watching the recent trail, surface ONE concrete, higher-leverage move to",
        "deliver or demonstrate value, or return {\"emit\": false} with a reason."
      ]
    }
  ]
}
```

Tune it in place and restart; no rebuild needed.

With no config file, the non-scenario settings default to:

```json
{ "provider": "claude", "captureScope": "fullScreen", "captureDenylist": [] }
```

…and with no `scenarios`, no nudges are produced — copy `huddle.config.example.json` to get
the defaults.

## Safeguards

- **Ephemeral screenshots.** Each captured frame is written to a temp file only for
  the CLI call and deleted immediately afterwards (success or failure). Only the text
  summary is stored — never the image.
- **Denylist.** See `captureDenylist` above. Pair it with `captureScope: activeWindow`
  to guarantee a sensitive foreground window is never sent.
- **Sensitive content.** Summaries never contain specific sensitive values — salaries,
  account/card numbers, passwords, medical values, or personal identifiers — the vision
  model describes the *kind* of thing, not the values. And by default
  (`skipSensitiveMoments: true`) a frame the model flags as sensitive is dropped entirely,
  storing no moment. Because scenarios read stored moments, this keeps sensitive content
  out of nudges, posts, and MCP queries too. This complements the denylist: the denylist
  catches known windows *before* capture; this catches sensitive *content* the denylist
  can't know about.
