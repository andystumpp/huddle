## Why

Huddle stores each screen's summary in the moment DB, and scenarios read that trail — so anything the vision model transcribes propagates onward into nudges, LinkedIn drafts, and MCP queries. A recent capture recorded compensation figures straight into the DB. We need content-level sensitive-data filtering, and it must not add a second model call (latency).

## What Changes

- The vision call now returns a small JSON object `{ "summary": …, "sensitive": true|false }` instead of plain text — the **same single call**, a richer reply.
- **The summary never contains specific sensitive values** (salaries, dollar amounts, account/card numbers, passwords, API keys, medical values, or personal identifiers) — the model describes the *kind* of thing ("a compensation letter"), not the values. This rule is always on and is the real guarantee.
- The model additionally **flags** whether the frame showed sensitive content.
- A config toggle **`skipSensitiveMoments` (default `true`)**: when a frame is flagged sensitive, the tick stores **nothing**. Set it `false` to keep the (value-free) summary for sensitive frames.
- Complements the existing `captureDenylist` (which suppresses known *windows* before capture) with content-level filtering (after capture, inside the same call).

Non-goals: persisting the sensitivity flag as a DB column (no schema change); a separate classifier pass; pixel-level redaction.

## Capabilities

### Modified Capabilities

- `moment-capture`: the vision call returns a summary plus a sensitivity flag; the summary is always free of specific sensitive values; a frame flagged sensitive is skipped by default.

## Impact

- **Code:** `MomentExtractor` (prompt + JSON parse + new `MomentVision` type; `ExtractAsync` returns `MomentVision`), the capture tick (skip check), `HuddleConfig` (`SkipSensitiveMoments`, default `true`), README.
- **No** DB/schema change (the flag is not persisted). **No** provider change (`DescribeImageAsync` still returns stdout). **No** extra model call.
