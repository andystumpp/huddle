## Why

Moment cards (Activity tab) and nudge cards (Nudges tab) show no time information, so the user can't tell whether an observation or suggestion is fresh or hours old. A compact relative timestamp ("3min ago") on each card answers "how old is this?" at a glance.

## What Changes

- Each **moment card** gains a relative-time label (e.g. `just now`, `3min ago`, `2h ago`, `5d ago`) derived from its `ts`, rendered in the existing footer row.
- Each **nudge card** gains the same relative-time label derived from its `ts`.
- A single shared formatter maps an age to its label string (`just now` under 60s, then minutes, hours, days).
- A single panel-level clock ticks roughly once a minute while the panel is open and refreshes every visible card's label so values stay current without re-rendering the lists. Cards subscribe on load and unsubscribe on unload (ItemsRepeater virtualizes them).
- No storage, schema, or model changes — both records already persist `ts` as ISO-8601 UTC.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `app-shell`: the *Moment card content* requirement adds a relative-timestamp element to the card and the periodic-refresh behavior.
- `nudges`: the *Nudge card* requirement adds the same relative-timestamp element and periodic-refresh behavior.

## Impact

- Code: `Controls/MomentCard.xaml(.cs)`, `Controls/NudgeCard.xaml(.cs)`, a new shared time formatter / clock helper, and `Views/PeekPanelWindow.xaml.cs` (own the once-a-minute tick).
- No database migration, no API changes, no new dependencies.
