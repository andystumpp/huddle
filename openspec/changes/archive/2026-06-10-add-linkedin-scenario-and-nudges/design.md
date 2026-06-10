## Context

ADR 0001 architecture: "Scenarios are prompts, not pipelines. A scenario is a prompt file that reads recent moments and decides whether to emit a nudge." We have moments end-to-end; we have a tick scheduler that fires every 3 minutes. What we don't have yet is a second Claude call after the moment lands — the one that actually produces something the user can *act on*.

This slice ships the smallest possible such call: one hardcoded scenario (LinkedIn posts), running hourly, returning a structured nudge or staying silent. No `.md` plugin loading, no `web_search`, no dismiss / save buttons, no per-scenario filter chips. Each of those is its own follow-up.

## Goals / Non-Goals

**Goals:**
- A `nudges` SQLite table, schema close to ADR 0001's nudge intent: `id`, `ts`, `scenario`, `title`, `body`, `sources` (JSON array of moment IDs).
- A `NudgeStore` mirroring `MomentStore`'s shape, with the same checkpoint-after-insert pattern so we don't repeat the WAL stranding problem.
- One hardcoded scenario, `LinkedInPostsScenario`, that:
  - Reads up to 20 recent moments (the last ~hour at 3-min cadence)
  - Sends a Claude text call with structured output (`{emit, title, body, sources}`)
  - On `emit: false`, persists nothing
  - On `emit: true`, persists a nudge row and prepends it to the panel's live collection
- Runs once per hour (throttled — every 20 ticks ≈ 60 min)
- A `NudgeCard` control that renders title (semibold, primary) + body (secondary, wrapped) + a small scenario tag.
- The Nudges tab's content area: empty state when no nudges, an `ItemsRepeater` of `NudgeCard`s when there are nudges.
- All emitted nudges are persisted, full stop. No filtering at the storage layer.

**Non-Goals:**
- **No `.md` plugin loading.** The scenario is a single C# class. Filesystem loading + frontmatter parsing lands in the next slice.
- **No tools / `web_search`.** Pure text call. Tools land with the "Latest trends" scenario.
- **No dismiss / save / filter UI on the Nudges tab.** The card has no action row. Add when we have multiple scenarios and the affordance earns its keep.
- **No abstraction for "scenarios".** Per `CLAUDE.md`'s SOLID guidance, we don't introduce an `IScenario` interface until there's a second scenario that needs it. Static class is fine for one.
- **No persisted `last_run`.** In-memory only. If the user closes and reopens within an hour, the scenario can run again — that's $0.012 in the worst case and we accept it.
- **No scheduled cap or per-day budget.** The hourly cadence is the only knob.
- **No empty-state for "scenario stayed silent."** Silent means we render nothing for that tick. The Nudges tab shows whatever's accumulated.

## Decisions

### D1. Schema

```sql
CREATE TABLE IF NOT EXISTS nudges (
    id        TEXT PRIMARY KEY,
    ts        TEXT NOT NULL,                -- ISO-8601 UTC, when emitted
    scenario  TEXT NOT NULL,                -- key like "linkedin-posts"
    title     TEXT NOT NULL,
    body      TEXT NOT NULL,
    sources   TEXT                          -- JSON array of moment IDs, nullable
);
CREATE INDEX IF NOT EXISTS idx_nudges_ts ON nudges(ts DESC);
```

Lives at the existing `%LOCALAPPDATA%\Huddle\huddle.db` alongside `moments`. Migration `002_nudges.sql`. `sources` stays a `TEXT` column holding JSON, not a separate table — at our scale a relational table for nudge↔moment is over-engineering.

### D2. Hardcoded scenario, no abstraction

`Scenarios/LinkedInPostsScenario.cs` is a static class with `Key`, `Name`, `Cadence` (TimeSpan), `TrailSize` (int), and a private `s_lastRun`. Three public methods: `IsDue(DateTimeOffset now)`, `RunAsync(IReadOnlyList<Moment> trail) -> Task<Nudge?>`, and (implicit) the constants.

When we add a second scenario (per the user's plan: "Latest trends", "Efficiency"), we extract a `Scenario` base class or interface then — not before. Per the YAGNI calibration in `CLAUDE.md`, the abstraction is earned when there's a second implementation that needs it.

### D3. Cadence — hourly, in-memory, throttled by default

`Cadence = TimeSpan.FromHours(1)`. The scenario's `IsDue(now)` returns `now - s_lastRun >= Cadence`. On first launch `s_lastRun = DateTimeOffset.MinValue`, so the first tick's `IsDue` is true and the scenario fires once shortly after startup.

In-memory `s_lastRun` means each app launch can run the scenario once even if the user closes + reopens within the hour. Acceptable cost (~$0.012 per launch worst case); persisting `last_run` is a YAGNI deferral.

### D4. Trail size — 20 moments

The LinkedIn scenario needs substantial trail to find anything genuinely post-worthy. 20 moments at 3-min cadence is roughly the last hour of work — the same span as the cadence. Larger trail = more context = more tokens; smaller trail = narrower view. 20 feels right and matches the panel's visible cap; we'll tune if posts come out shallow.

### D5. System prompt

```
You are Huddle's LinkedIn Posts scenario.

You see the user's last 20 moments — the trail of what they've been doing
in the past hour. The user is a principal-level software architect. Their
LinkedIn audience cares about thought leadership on AI-assisted
development: real challenges shipping with AI, what they've actually
learned, and how to build software in this era — not vibes, not hype.

Your job: when the trail shows a genuinely post-worthy insight — a
specific challenge they navigated, a sharp opinion that fell out of
their work, a learned heuristic they've now applied — draft one
LinkedIn post idea. Otherwise stay silent.

A good post idea:
- Anchors in a specific concrete thing the user actually did. Not
  generic "AI is changing dev". The moments show real work; the post
  references that work.
- Reads in a principal architect's voice: opinionated, specific, shows
  the seams. Never motivational. No emojis. No hashtags.
- Has a tweet-sized hook (~1 sentence) that earns the click, then 2-3
  sentences of substance. Title field = hook. Body field = substance.
- Avoids "I just used AI to..." narcissism. Frames the insight, not
  the user's brag.
- Doesn't repeat a post you would have proposed from earlier moments
  in the trail (the user has seen those already).

If the trail shows only routine work — context switching, fixing a
typo, scrolling — return {"emit": false}. Silent beats a forced post.

When you emit, sources should be the IDs of 1-3 moments that most
justify the post — the actual work that earned the idea.
```

### D6. Structured output

`output_config.format` with a JSON schema:

```json
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "emit":    { "type": "boolean" },
    "title":   { "type": "string" },
    "body":    { "type": "string" },
    "sources": { "type": "array", "items": { "type": "string" } }
  },
  "required": ["emit"]
}
```

`emit` is the only required field; `title` / `body` / `sources` are filled when `emit: true`. We `JsonSerializer.Deserialize<NudgeDraft>` the response text. If `emit: false` (or missing required fields when `emit: true`), we silently skip; if the API call throws, log via `Debug.WriteLine` and continue.

### D7. User content shape

A single text block (no image — scenarios don't need the screenshot, only the moment summaries that already condensed it):

```
Recent moments (newest first):
- 2 min ago, Code.exe ("MomentExtractor.cs — huddle"): m_01HXA… You're verifying the structured-output wiring landed correctly.
- 5 min ago, Code.exe ("Test Explorer"): m_01HX9… You're running the new tests against...
...

The user is a principal-level software architect; you saw the trail above. Draft a LinkedIn post idea or stay silent per the system prompt.
```

Moment IDs are included so `sources` references work cleanly. Format identical to the moment-trail format from `infer-user-intent`, with the moment ID prefixed so the model can cite it.

### D8. Where the runner lives

`PeekPanelWindow.OnSchedulerTick`, after the new moment is persisted and inserted into `_moments`. Single block of code:

```csharp
if (LinkedInPostsScenario.IsDue(DateTimeOffset.UtcNow))
{
    var trail = await MomentStore.RecentAsync(LinkedInPostsScenario.TrailSize);
    var nudge = await LinkedInPostsScenario.RunAsync(trail);
    if (nudge is not null)
    {
        await NudgeStore.AddAsync(nudge);
        _nudges.Insert(0, nudge);
        while (_nudges.Count > MaxVisibleNudges) _nudges.RemoveAt(_nudges.Count - 1);
        UpdateNudgeCount();
    }
}
```

When a second scenario lands, this becomes a `foreach (var scenario in _scenarios)` loop. Until then it's one block — no shape we don't need.

### D9. `NudgeCard` visual

Same card frame as `MomentCard` (rounded 10 px, subtle white-tint background, 1 px border). Inside:
- A small scenario tag at the top — colored dot (use the social violet for LinkedIn for now) + uppercase scenario name (`LINKEDIN POSTS`) in `T3` color, letter-spaced.
- Title text: `FontSize="14.5"`, `FontWeight="SemiBold"`, color `T1`, two-line max with `TextTrimming="CharacterEllipsis"`.
- Body text: `FontSize="12.5"`, `LineHeight="20"`, color `T2`, wraps.
- No app tile, no footer row, no action buttons. Title carries the hook; body carries the substance.

### D10. Nudges tab content swap

The Nudges tab content currently shows the empty state unconditionally. We change it to:

```xaml
<Grid x:Name="NudgesContent" Visibility="Collapsed">
    <StackPanel x:Name="NudgesEmptyState" ... />  <!-- existing empty state -->
    <ScrollViewer x:Name="NudgesScroll" Visibility="Collapsed">
        <ItemsRepeater x:Name="NudgesRepeater" ... />
    </ScrollViewer>
</Grid>
```

In code-behind, after loading nudges from `NudgeStore.RecentAsync(20)`, set `NudgesEmptyState.Visibility = Collapsed` and `NudgesScroll.Visibility = Visible` when `_nudges.Count > 0`, and vice versa. Mirror the toggle in the orchestration code when the first nudge is inserted.

## Risks / Trade-offs

- **[Scenario prompt produces shallow / forced posts even when emitting]** → The voice prompt is the lever. We'll see the first emitted nudge within an hour of running the app and tune from there. The hourly cadence keeps the feedback loop slow but cheap.
- **[In-memory `last_run` means open/close hammering can rerun the scenario]** → Accepted. Worst case ~$0.012 per launch. Persisting `last_run` in DB is a one-liner when it earns its keep.
- **[20-moment trail × Sonnet 4.6 = real-ish input token cost]** → ~2,500 input tokens per call. At $3/MT input = ~$0.0075 input + ~$0.0038 output = ~$0.011. Within the ballpark named in the proposal.
- **[Conditional empty-state in NudgesContent is more logic than the current single child]** → Minor. Wrapped in `UpdateNudgesSurface()` so the visibility flip lives in one method.
- **[The hardcoded scenario can't be tuned without a rebuild]** → Accepted and the entire point of the next slice. This slice's prompt is checked into the source so we can edit and rebuild while iterating on voice.

## Open Questions

- "LINKEDIN POSTS" as the visible tag — too brand-y, too informal? Could just be "POSTS" or "DRAFT". Easy to flip.
- Should the scenario refuse to emit two nudges back-to-back about the same insight? The prompt says "don't repeat" but we don't enforce. Accept for slice 1; revisit if duplicates appear.
- Should we surface a "scenario stayed silent" tally (e.g. "Last checked 7 min ago — nothing post-worthy yet") so the user knows the scenario is alive? Open question; not in this slice. A simple `last_checked` timestamp on the empty state could read "Last checked 7 min ago".
