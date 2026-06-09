# ADR 0001: High-Level Architecture

- **Status:** Accepted
- **Date:** 2026-06-09

## Context

Huddle is a Windows desktop app that watches the user's screen, understands what they're doing, and delivers scenario-driven nudges (starting with social-media ideas and efficiency coaching). See [product/outline.md](../product/outline.md).

The north star: *"Did Huddle tell me something today that I'm glad I knew, that I wouldn't have thought of on my own?"* Every architectural choice is filtered through that question and YAGNI — we cut anything that isn't load-bearing for v1.

Two decisions were settled before this ADR and constrain everything below:

1. **Vision inference runs in Claude (cloud).** No local VLM, no cheap-model gate.
2. **Scenarios are prompts**, not pipelines. A scenario is a prompt file that reads recent moments and decides whether to emit a nudge.

## Decision

### End-to-end flow

```
Tick (every 3 min, paused when system idle)
  │
  ├─ capture frame + foreground app/title
  │
  ├─ Claude vision call
  │     in:  frame + last N moment summaries
  │     out: new moment { summary }
  │
  ├─ write moment → SQLite
  │
  ├─ for each enabled scenario prompt:
  │     Claude text call
  │       in:  scenario prompt + recent moments
  │       out: nudge or nothing
  │     write nudges → SQLite
  │
  └─ Tray app + peek panel reads SQLite
        (latest nudge on top, scrollable history below)
```

### Components

**Tick loop**
- Single cadence: every 3 minutes. Skip when system is idle.
- Windows Graphics Capture API for the frame.
- Foreground window app name + title read deterministically (no vision needed).
- Global pause button. No app/URL exclusion list yet.

**Understand (vision pass)**
- One Claude vision call per tick.
- Input: the frame plus the last N moment summaries (start small, e.g. N=6).
- Output: a new moment.
- The frame is **discarded** after the call. Only the moment row is persisted. We can never re-run a different prompt over the same frame — accepted trade-off.

**Moment schema** (settled in conversation, repeated here for the record):

```json
{
  "id": "01HXZ8K3...",
  "ts": "2026-06-09T14:22:07Z",
  "app": "Code.exe",
  "window_title": "outline.md — huddle — Visual Studio Code",
  "summary": "User is rewriting the North Star paragraph; has rephrased the success metric three times in the last two minutes."
}
```

Five fields. `app` and `window_title` are deterministic and let scenarios filter cheaply. `summary` carries all understanding in prose. No `signals`, `entities`, `continuity`, or `confidence` — the scenario prompt does its own pattern-spotting over recent summaries.

**Storage**
- One SQLite file, local.
- Two tables: `moments`, `nudges`.

**Scenarios**
- Each scenario is a `.md` file in a folder containing a prompt.
- Ship two: `social-media-ideas.md`, `efficiency-coach.md`.
- Scenarios run on the same 3-minute tick as capture: on each tick, every enabled scenario gets called with the recent moments and decides whether to emit a nudge. One cadence, one knob.

**Shell**
- WinUI 3 + .NET / C# tray app.
- Peek panel: latest nudge at the top, scrollable history below.
- No Windows toasts in v1 — the peek panel is the only surface.

### Stack

| Layer | Choice | Why |
|---|---|---|
| Shell | WinUI 3 + .NET / C# | Native feel, low overhead, good tray + acrylic panel story, cheap Graphics Capture interop. Matches the "ambient, part of the desktop" north star better than Electron/Tauri. |
| Capture | Windows Graphics Capture API | First-party, supported, low overhead. |
| Vision + scenarios | Claude API | Decided. |
| Storage | SQLite | Local-first, zero ops, fits the data shape. |

### Explicitly out of scope for v1

Add only when a real need shows up:

- Adaptive capture cadence
- Local or cheap-model pre-filter before the vision call
- Toast notifications
- App / URL exclusion lists (pause button covers it)
- Trigger rules or per-scenario filters (the prompt decides)
- Cross-moment entity linking, continuity stitching, structured signal vocabularies
- Scenario marketplace, sharing, multi-profile

## Consequences

**Good**
- Schema and flow are small enough to fit in your head. Easy to change.
- Scenarios are user-editable text files — adding a new lens is writing a prompt, not shipping code.
- All user data stays local except the frames sent to the Claude API.

**Accepted costs**
- API spend scales linearly with active-use time: one vision call + N scenario calls per 3-minute tick. Worth measuring early.
- Discarding frames after the vision call means we cannot retroactively re-interpret past activity with a new scenario prompt — only future moments will benefit from a new scenario.
- A 3-minute tick will miss short-window patterns (e.g. "revised three times in two minutes"). Efficiency-coach in particular may feel weak as a result. Accepted until we have evidence it's a real problem; the fix is decoupling capture and scenario cadences.

**Things to revisit**
- Decoupling capture cadence from scenario cadence (e.g. capture every 30–60s, scenarios every 3 min) if efficiency-coach feels weak in practice.
- Tick cadence (fixed → adaptive) once we have data on hit-rate vs. cost.
- Frame retention, if a use case for re-interpreting history emerges.
- A cheap pre-filter (heuristic or small local model) if vision-call cost becomes the dominant constraint.
