## Context

The LinkedIn scenario shipped without an abstraction, deliberately, per the project's "earn the interface before introducing it" rule. We're at the moment that earns it: the user wants a second scenario (Achievements), and the natural shape is "build the abstraction now, plug both in, and structure the code so a third scenario is a one-class addition."

The two scenarios sharing the same orchestrator also lets us add a small but high-value piece of context to every scenario call: the most-recently-emitted nudges *from that same scenario*. LinkedIn's prompt already says "don't repeat what you've already posted"; until now we couldn't give it the receipts. Achievements needs the same dedup or it'll log the same shipped PR every hour.

## Goals / Non-Goals

**Goals:**

- A `Scenario` abstract class that owns the boilerplate (cadence throttle, mutex around runs, the template `RunAsync` that wraps `ExecuteAsync` with `_lastRun` housekeeping).
- A registry pattern (`ScenarioRegistry.All`, `GetByKey`) that the panel iterates per tick and that the UI reads for display info.
- Per-scenario visual identity (display name + accent color) so the existing Nudge card stops hardcoding "LINKEDIN POSTS".
- Per-scenario prior-nudges context: scenarios see the last N nudges they emitted, dedup happens in the prompt rather than in code.
- One concrete second scenario — Achievements — that exercises the abstraction.
- Manual trigger fires all scenarios at once, bypassing throttle, surfaces an aggregate status.

**Non-Goals:**

- **No `.md` plugin loading.** Registry is hardcoded for this change. The All list can be swapped to a filesystem loader in a later change without touching consumers.
- **No new dependencies.** Stays on the existing Anthropic SDK + Microsoft.Data.Sqlite + Microsoft.Win32.SystemEvents stack.
- **No per-scenario UI controls.** Single "Run all now" button; per-scenario enable/disable toggles land with the .md plugin loader (when there's a real need to toggle scenarios that aren't in code).
- **No structured Body / observation split, no card layout change.** Defer until we know what shape the surfacing wants.
- **No persistent `last_run`.** Still in-memory per scenario; if the user closes and re-opens within an hour, scenarios can run again. Acceptable cost.
- **No date-bounded "today" semantics for Achievements.** Prior-nudges context is bounded by count (N=20), not by date. If achievements span days that 20 covers everything reasonable; date filtering is a tiny SQLite change when the need is real.

## Decisions

### D1. `Scenario` abstract class

```csharp
internal abstract class Scenario
{
    public abstract string Key { get; }
    public abstract string Name { get; }              // human-readable
    public abstract string DisplayName { get; }       // tag (uppercase, e.g. "ACHIEVEMENTS")
    public abstract string AccentColorHex { get; }    // dot color, e.g. "#54D2A6"
    public abstract TimeSpan Cadence { get; }
    public abstract int TrailSize { get; }
    public virtual int PriorNudgesSize => 10;

    private DateTimeOffset _lastRun = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsDue(DateTimeOffset now) => now - _lastRun >= Cadence;

    public async Task<ScenarioResult> RunAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _lastRun = DateTimeOffset.UtcNow;
            return await ExecuteAsync(trail, priorNudges, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] scenario {Key} failed: {ex.GetType().Name}: {ex.Message}");
            return new ScenarioResult(null, null);
        }
        finally { _gate.Release(); }
    }

    protected abstract Task<ScenarioResult> ExecuteAsync(
        IReadOnlyList<Moment> trail,
        IReadOnlyList<Nudge> priorNudges,
        CancellationToken ct);
}
```

- `RunAsync` is the template: always updates `_lastRun` (whether the run emitted, stayed silent, or threw), serializes concurrent attempts, and swallows exceptions at the boundary so one scenario can't take down the others.
- `ExecuteAsync` is the per-scenario body: build the Claude call, parse the structured output, return a `ScenarioResult`.
- The visual identity properties (`DisplayName`, `AccentColorHex`) live on the scenario itself so `NudgeCard` has a single source of truth.

### D2. `ScenarioRegistry`

```csharp
internal static class ScenarioRegistry
{
    public static IReadOnlyList<Scenario> All { get; } = new Scenario[]
    {
        new LinkedInPostsScenario(),
        new AchievementsScenario(),
    };

    public static Scenario? GetByKey(string key) =>
        All.FirstOrDefault(s => s.Key == key);
}
```

Hardcoded for now. When `.md` plugin loading lands, the All initializer changes to read the filesystem; consumers stay the same.

### D3. Prior nudges in the prompt — implementation

The Achievements prompt explicitly needs to see what it's already emitted today; LinkedIn's "don't repeat" clause was aspirational until now. We pass priorNudges (most-recent first, capped per-scenario at `PriorNudgesSize`) and render them in a dedicated user-message block:

```
Previously emitted by this scenario (newest first):
- 2h ago: "Shipped checkpoint-after-write fix" — Forced a WAL TRUNCATE after every INSERT so force-kill loops can't lose committed rows.
- 4h ago: "Refactored the moments tab" — Renamed PatternSeed to MomentStore-backed observations; rendered live via ObservableCollection.

(rest of the user message — recent moments, current foreground — unchanged)
```

Each scenario's `ExecuteAsync` decides exactly how to inject this — keeping format flexible per scenario. The achievements scenario uses the format above; LinkedIn can choose to abbreviate further. Empty list → block omitted.

### D4. `AchievementsScenario` specifics

- **Cadence:** 1 hour.
- **TrailSize:** 60 (~3 hours back at 3-min cadence). Big enough to spot the achievement; small enough that the prompt isn't drowning.
- **PriorNudgesSize:** 20 — comfortably covers a day's emits even if the achievement scenario gets chatty.
- **DisplayName:** `ACHIEVEMENTS`.
- **AccentColorHex:** `#54D2A6` (the existing efficiency-teal — already in the palette, semantically right for "things accomplished").
- **System prompt:** asks for *one* concrete achievement at a time. What counts:
  - Shipped: PR merged, feature deployed, doc published
  - Decided: design call made, scope cut, tradeoff chosen
  - Resolved: bug fixed, outage handled, blocker cleared
  - Learned: pattern adopted, belief updated, gotcha discovered
  - Moved: draft → review, idea → spec, question → answer

  Plain past tense for completed things ("You shipped X"), present for ongoing decisions ("You decided X"). No motivational language. No emojis. Confident commits when the trail is clear; hedged when ambiguous. Returns `{emit:false, reason}` when the trail shows nothing new since the prior emits.

- **Title field:** the achievement in one short line. **Body field:** 1–2 sentences of context (what specifically, what it unblocks or moves to next). **Sources:** moment IDs that show the achievement.

### D5. Orchestration in `PeekPanelWindow.OnSchedulerTick`

Replace the hardcoded LinkedIn block with:

```csharp
var now = DateTimeOffset.UtcNow;
foreach (var scenario in ScenarioRegistry.All)
{
    if (!scenario.IsDue(now)) continue;

    var trail = await MomentStore.RecentAsync(scenario.TrailSize);
    var priorNudges = await NudgeStore.RecentByScenarioAsync(scenario.Key, scenario.PriorNudgesSize);
    var result = await scenario.RunAsync(trail, priorNudges);
    if (result.Nudge is null) continue;

    await NudgeStore.AddAsync(result.Nudge);
    _nudges.Insert(0, result.Nudge);
    while (_nudges.Count > MaxVisibleNudges) _nudges.RemoveAt(_nudges.Count - 1);
    UpdateNudgesSurface();
}
```

Note: scenarios run **sequentially** (not in parallel). At a single user's cadence this is fine — sequential is also the cheaper failure mode (one slow scenario doesn't make the next one's tick overlap). If perf ever matters, parallelize then.

### D6. Manual trigger — run all, aggregate status

The "Run scenarios now" button (existing play glyph on the Nudges tab header) iterates the registry once, bypassing each scenario's `IsDue` check. Each scenario still updates its own `_lastRun` (so the next *scheduled* tick is throttled normally).

Status text after the run:
- `Run complete: 2 emitted, 1 silent` when at least one scenario succeeded
- `Silent: <first scenario's reason>` when nothing emitted but at least one scenario produced a reason
- `Scenario stayed silent` as a final fallback

### D7. `NudgeCard` reads display info from the registry

`Apply()` now does:

```csharp
var meta = ScenarioRegistry.GetByKey(Nudge.Scenario);
ScenarioTagText.Text = meta?.DisplayName ?? Nudge.Scenario.ToUpperInvariant();
ScenarioDot.Fill = ParseHex(meta?.AccentColorHex ?? "#C58BFF");
```

No more switch statement in the control. New scenarios automatically get their tag + dot color without touching XAML.

### D8. `NudgeStore.RecentByScenarioAsync`

```csharp
public static async Task<IReadOnlyList<Nudge>> RecentByScenarioAsync(string scenario, int limit)
{
    // SELECT ... FROM nudges WHERE scenario = $key ORDER BY ts DESC LIMIT $limit
    ...
}
```

Mirrors `RecentAsync` but with a `WHERE scenario = $key`. Reuses the JSON deserialization path for `sources`. The existing `idx_nudges_ts` covers `ORDER BY ts DESC`; a small index on `(scenario, ts DESC)` would optimize this further, but at our row volume the optimizer is fine — defer the index until we see slow queries.

## Risks / Trade-offs

- **[Two scenarios firing on the same tick double the API spend at that moment]** → Accepted; the cadence policy is what bounds total spend, and both are hourly. Pause-on-lock and the manual pause are the user's controls.
- **[Achievement prompt over-emits the same shipped PR every hour]** → Mitigated by the prior-nudges context plus the explicit "don't repeat" clause. If we still see duplicates, tighten the prompt.
- **[Refactor changes LinkedIn's internal class shape]** → Internal-only; no public surface broken. The persisted nudges remain the same on disk.
- **[Registry hardcoded — adding a third scenario still needs a recompile]** → Accepted for this slice. The point of the registry pattern is to make the *consumer* extension-friendly; the *source of scenarios* becomes pluggable in the next change.
- **[Sequential scenario execution is slower if both emit]** → At 1 hour cadence with two scenarios per scheduled tick, the user might wait ~6–10 s instead of ~3–5. Acceptable.

## Open Questions

- Should the manual trigger spawn its own "run summary" card (e.g., a transient toast) so the user knows what each scenario decided? Defer — for now the aggregate status line is enough.
- Should Achievements' cadence be longer than LinkedIn's (e.g., 2 hours) so the dedup window is wider? Hourly to start, easy to lengthen. Watch the first day's emits.
- Should we add a per-scenario filter on the Nudges tab? Original prototype had chips; we deferred. Two scenarios is still the wrong moment — defer until 3+ exist.
