## Why

The current moment prompt asks Claude to describe what's on screen, and that's what we get back — faithful screen reads like "you're reviewing the Inlinr Engineering Lifecycle HTML doc while a chat panel shows an AI agent that just opened PR #230." Accurate, but flat: it names what's visible, not what the user is trying to accomplish. Andy's whole product bet is that a watching kibitzer should answer *"why are you on this screen?"*, not just *"what's on this screen?"*.

Two cheap levers make the jump from description to intent. First, change the prompt to ask for the user's *goal* rather than the screen's *contents*, with a hedged voice so a wrong guess reads thoughtful instead of overconfident. Second, give the model the trail — the last N moment summaries — so it can read trajectory (this isn't a casual doc scroll; it's the third tick in a row on the same branch, two ticks ago an agent emitted a PR, now the user's on the lifecycle doc — they're verifying the refactor end-to-end). One frame can't see that. Six can.

## What Changes

- **Modify** the system prompt in `MomentExtractor` to ask for what the user is *trying to accomplish*, framed as "you're trying to X" / "you're verifying X" rather than "you're looking at X". Voice stays dry / observant / second-person; add explicit guidance to hedge ("you're likely…", "you seem to be…") when the trail doesn't pin the goal down.
- **Modify** the user-message content to include a "Recent moments" block listing the last 6 moments (newest first), each with relative time, app, abbreviated window title, and the prior summary text. The screenshot + foreground-app/title still come along.
- **Re-infer fresh each tick.** No goal stickiness. Each call gets the latest trail; if the user pivots, the next moment reflects that. No state carried by the panel beyond what's already in SQLite.
- Pull the trail from `MomentStore.RecentAsync(6)` immediately before each vision call. The store is already there.
- Skip the trail block entirely on the first-ever capture (empty store) — the moment becomes a single-shot intent inference from the current frame alone.

## Capabilities

### Modified Capabilities

- `moment-capture`: the "Claude vision call" requirement gets re-cast — system prompt now asks for intent (hedged), user content now includes the trailing-moments block. Schema, store, tick, and chrome are unchanged.

## Impact

- `src/Huddle.App/Vision/MomentExtractor.cs` — rewrite the system prompt; change `ExtractAsync` to accept `IReadOnlyList<Moment> recent` (or fetch from `MomentStore` itself — see design.md D2); build the recent-moments text block before the foreground line; format relative time client-side.
- `src/Huddle.App/Views/PeekPanelWindow.xaml.cs` — `OnSchedulerTick` reads recent moments from `MomentStore.RecentAsync(6)` *before* calling `MomentExtractor.ExtractAsync` and passes them in.
- No DB schema change. No UI change. No new dependencies.

## Cost note

Adds ~6 × 80 ≈ 500 input tokens per call (the trail summaries). At Sonnet 4.6 that's ~$0.0015 extra, putting per-snapshot cost around $0.0075–$0.008 from ~$0.006. At a 3-min tick that's ~$0.16/hour vs the current ~$0.12. Acceptable for the upgrade in quality.
