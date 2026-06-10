## Context

Moment cards and nudge cards both bind a record carrying a `ts` (`DateTimeOffset`, ISO-8601 UTC), but neither renders it. Both lists are rendered with virtualizing `ItemsRepeater`s (`MomentsRepeater`, `NudgesRepeater`) whose item templates instantiate `MomentCard` / `NudgeCard` and bind the record. The panel (`PeekPanelWindow`) already owns several `DispatcherTimer`s (status, slide, hover, read-grace). New cards arrive at the top in real time while the panel is open, so any age label must keep updating, not just compute once.

## Goals / Non-Goals

**Goals:**
- A compact relative-age label on every moment and nudge card, derived from `ts`.
- Labels stay accurate while the panel sits open, refreshed on a single shared cadence.
- One formatter, one clock — no duplication between the two card types.

**Non-Goals:**
- No storage, schema, or model changes (`ts` is already persisted).
- No absolute timestamp, tooltip, or locale-aware formatting in this change.
- No seconds-granularity label — under a minute is "just now".

## Decisions

### D1: Shared formatter — `RelativeTime.Format(DateTimeOffset ts, DateTimeOffset now)`

A static helper (e.g. `Huddle.Time.RelativeTime`) maps an age to its label: `< 60s → "just now"`, `< 60min → "{m}min ago"`, `< 24h → "{h}h ago"`, else `"{d}d ago"`. Pure and `now`-injectable so it's trivially unit-testable and the spec scenarios map 1:1 to cases. Both cards call it; neither owns the rule.

*Alternative considered:* `DateTimeOffset` extension method or per-card private formatting — rejected as it either hides the rule or duplicates it.

### D2: Shared once-a-minute clock the cards subscribe to, not a per-card timer

Because `ItemsRepeater` virtualizes, a per-card `DispatcherTimer` would churn with scroll and leak if not torn down. Instead a single static clock (e.g. `RelativeTime.Tick` event, driven by one `DispatcherTimer` owned by the panel) fires roughly once per minute. Each card subscribes in its `Loaded` handler and unsubscribes in `Unloaded`, recomputing its own label on each tick (and once on `Apply()`). One timer regardless of card count; virtualized-away cards detach cleanly.

*Alternatives considered:*
- Reuse the existing 1-second `_statusTimer` to walk realized children — couples the shell to card internals and reaches across the repeater's virtualization. Rejected.
- Bind via a converter on a ticking property — converters can't easily be re-pulsed without a bound source notifying, which lands back at the same shared-clock plumbing with more indirection.

The panel starts the clock when shown and stops it when hidden/closed, so it costs nothing while the panel is off-screen. (A 60s tick can lag a label by up to ~60s, which is acceptable at this granularity.)

### D3: Placement

- **Moment card:** the timestamp joins the existing footer row (`AppTile` + window title). Layout becomes tile | title (`*`, ellipsis-trimmed) | timestamp (`Auto`, right). The title's trim column yields so the timestamp stays whole.
- **Nudge card:** the timestamp sits on the existing scenario-tag header row, right-aligned opposite the tag — reads as metadata, away from the title/body and the star/copy footer.

Both use the existing muted secondary text treatment (`~#6BFFFFFF`, ~11px) so it reads as chrome, not content.

## Risks / Trade-offs

- **A 60s tick means a label can be up to ~60s stale.** → Acceptable at minute granularity; "just now" → "1min ago" lagging by under a minute is invisible to the user.
- **`Loaded`/`Unloaded` subscribe/unsubscribe must be balanced or the static event leaks card references.** → Symmetric handlers; unsubscribe unconditionally in `Unloaded`. Static event holds only currently-realized cards.
- **Clock drift / system clock changes** could momentarily misorder labels. → Out of scope; labels are best-effort and recompute each tick from `DateTimeOffset.Now`.
