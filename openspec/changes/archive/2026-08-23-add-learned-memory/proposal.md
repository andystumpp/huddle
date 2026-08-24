## Why

Scenarios read only a rolling window of the last N moments plus the static, hand-typed `context`; nothing accumulates or is learned. Huddle can't connect today to weeks ago, and re-derives context from raw screenshots every run (window blindness, no accumulation — see issue #45). This adds a **learned, durable profile** distilled from the moment trail, so every scenario reasons with your ongoing projects, decisions, and preferences instead of just the last hour.

## What Changes

- A **daily reflection job** — separate from scenarios, not a scenario itself — reads the recent moment trail plus the current profile and rewrites an evolving **`profile.md`**.
- The profile is a single markdown document stored at **`%LOCALAPPDATA%\Huddle\profile.md`** (next to `huddle.config.json` and `huddle.db`), so the user can open, read, and edit it directly. Not in the database — a file.
- Every scenario's system prompt becomes **static `context` + learned profile + the scenario's own prompt** (prepended in `ConfiguredScenario`).
- Always on; **no configuration** for this feature (built-in daily job, fixed behavior).

Non-goals: structured/entry-level memory with explicit ADD/UPDATE/DELETE and recency decay (the MVP lets the LLM maintain a single doc); embeddings / semantic retrieval; applying the profile to the vision/moment prompt; a config surface (cadence/model/prompt overrides).

## Capabilities

### New Capabilities

- `learned-memory`: a daily reflection job that distills the moment trail into a durable, user-editable `profile.md`, which is prepended to every scenario's system prompt.

## Impact

- **Code:** a reflection job (run from the existing tick loop, sibling to `RunScenariosAsync`); a `ProfileStore` in `Huddle.Core` that reads/writes `profile.md`; `ConfiguredScenario` prepends the profile (after the static `context`). The reflection uses the existing `ICliProvider.CompleteAsync` with a trivial `{ "profile": … }` schema (no new provider method).
- **Storage:** a new `profile.md` file in `%LOCALAPPDATA%\Huddle` — **no** database/schema change (it's a file, deliberately, so it's human-readable/editable).
- **No config**, no provider change, no change to vision. Backward compatible: with no `profile.md` yet (cold start), scenarios just get an empty profile block until the first reflection runs.
