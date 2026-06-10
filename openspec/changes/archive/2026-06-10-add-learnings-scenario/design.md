## Context

The Scenario abstraction is already in place ([Scenario.cs](../../../src/Huddle.App/Scenarios/Scenario.cs)) and two scenarios use it: `LinkedInPostsScenario` (Opus, hourly, 20 moments) and `AchievementsScenario` (Sonnet, hourly, 60 moments). Adding a third — Learnings — is purely additive: a new file deriving from `Scenario`, one line in `ScenarioRegistry`, no DB / UI / dependency changes.

The interesting design question is not *how* to add it (mechanically the path is clear) but *how to differentiate it from Achievements*. Both can reasonably emit nudges with the same surface shape; the value is in the prompt's framing and the wider lens.

## Goals / Non-Goals

**Goals:**
- Surface a once-per-day "here's what you learned today" nudge anchored in concrete moments.
- Use a day-sized trail (~200 moments) so the model can reason across the whole day, not just the last hour.
- Use Opus 4.8 — picking out a genuine learning thread from a day of noise is a reasoning-heavy task, similar in shape to the LinkedIn post selection.
- Stay distinct from Achievements: Achievements emits when a *thing happened*; Learnings emits when *the user updated a belief or technique*.

**Non-Goals:**
- Persisting the 24-hour throttle across app restarts. In-memory is consistent with the other scenarios; the prior-nudges dedup handles the restart case.
- Multiple learnings per day. One nudge per day is the contract — if the model finds nothing learning-shaped, it stays silent for the day.
- Editing or dismissing learnings. NudgeStore is append-only, same as the other scenarios.
- A separate "Learnings" tab or filtered view. The card already tags by scenario via the registry, so it surfaces in the unified Nudges list with the warm-amber accent.

## Decisions

### D1. Subclass `Scenario`; mirror `AchievementsScenario` skeleton

The base class already handles throttle, concurrent-run gate, and the call template. The new scenario is a near-clone of `AchievementsScenario.cs`:

```csharp
internal sealed class LearningsScenario : Scenario
{
    public override string Key => "learnings";
    public override string Name => "Learnings";
    public override string DisplayName => "LEARNINGS";
    public override string AccentColorHex => "#F5C56C";
    public override TimeSpan Cadence => TimeSpan.FromHours(24);
    public override int TrailSize => 200;
    public override int PriorNudgesSize => 5;
    public override Model ModelId => Model.ClaudeOpus4_8;
    // ExecuteAsync + BuildUserText: same shape as AchievementsScenario
}
```

**Alternative considered:** factor the boilerplate out of `Achievements` / `LinkedIn` / `Learnings` into a `ClaudeNudgeScenario` mid-base. **Rejected** — DRY-too-early. We've now seen the pattern three times but each scenario tweaks `BuildUserText` and we don't have a real reason to extract yet. CLAUDE.md says "When something does grow a second variant, that's the moment for an interface — not before." A third near-clone earns the extraction *next* time, not this time.

### D2. Cadence = 24h, throttle stays in-memory

Matches the other scenarios. Risk: app restart re-fires within the day. Mitigated by:
- The model sees `Previously emitted today` from `RecentByScenarioAsync` and is told to stay silent if today's learnings are already captured.
- One extra Opus call per restart is acceptable cost.

**Alternative considered:** persist `_lastRun` to SQLite. **Rejected** — YAGNI. The dedup path is already correct behavior, persistence is extra surface area for a problem that has not yet bitten.

### D3. TrailSize = 200

At a 3-minute capture cadence, 200 moments ≈ 10 hours of trail. That comfortably spans a normal workday with headroom for longer days. Achievements uses 60 (one hour); LinkedIn uses 20 (last hour focus). 200 fits the "whole day" framing.

200 moments × ~150 chars summary ≈ 30 KB of trail text — well within Opus's input window. No truncation logic needed.

### D4. PriorNudgesSize = 5

A once-a-day scenario doesn't accumulate many prior nudges. Five is enough to dedup against a long-running app session over the last few days while keeping the prompt tight.

### D5. Model = Opus 4.8

LinkedIn already uses Opus for the same reason: "find the sharp opinion in the noise" is a reasoning-heavy task. "What did the user learn today" has the same shape — most of the day is mechanics, the learning thread is the needle. Sonnet would underfit.

### D6. System prompt — focus only on the *learned* axis

The prompt explicitly carves a sharp boundary from Achievements:

> Achievements answers "what got done." Learnings answers "how did your *understanding* change." A bug got fixed → achievement. A new gotcha now lives in your head that wasn't there this morning → learning.

What counts as a learning:
- A new pattern adopted (you started using X where you didn't yesterday)
- A previous belief updated (you thought X, now you think Y because Z)
- A gotcha discovered (a behavior surprised you and now you know about it)
- A heuristic refined (your rule-of-thumb for when to do X got sharper)
- A tool / API / library / person you didn't know about, that you now do

Voice: plain, past-tense, second-person, anchored to concrete moments and IDs. Same no-emoji / no-motivational rules as Achievements. If today's trail shows mostly grinding through known patterns with no learning thread, stay silent with a one-line reason.

### D7. Schema reuse

Reuses `ScenarioPromptHelpers.BuildNudgeDraftSchema()` and the existing `NudgeDraft` record. No new schema.

### D8. Registry placement

Append to `ScenarioRegistry.All` after `AchievementsScenario`. Order matters only for the manual-trigger evaluation order; visually the panel sorts by `ts DESC`.

## Risks / Trade-offs

- **Risk:** Learnings and Achievements overlap on "learned" — Achievements' prompt already lists "Learned" as one of its categories.
  → Mitigation: the Learnings prompt sharpens the boundary (D6) and the Learnings card has its own tag. Some overlap is acceptable — Achievements covers learnings as one of five lenses at hourly cadence; Learnings is the dedicated daily lens. Users get both perspectives, not duplicates: hourly Achievements rarely picks "learned" as the strongest signal of the hour, and the daily Learnings reads the whole day.

- **Risk:** 200-moment trails balloon the prompt and cost more per call.
  → Mitigation: ~30 KB is small for Opus. One call per day means cost is bounded. Trail size is a constant we can tune if it bites.

- **Risk:** First run of the day on app launch can fire immediately (since `_lastRun = MinValue`), which may not be the right moment (user just started, no day to recap yet).
  → Mitigation: accept it for v1. The scenario sees the prior day's moments via the trail and will either find a learning or stay silent. If this proves annoying, add a "skip if trail spans less than N hours" guard later.

- **Risk:** Adding a third scenario starts to feel like noise on the Nudges list.
  → Mitigation: out of scope for this change. If volume becomes a problem, the next iteration adds filtering / dismissal — that's already on the roadmap implicitly.
