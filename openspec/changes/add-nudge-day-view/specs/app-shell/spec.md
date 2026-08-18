## ADDED Requirements

### Requirement: Nudges tab day-grouped review

The Nudges tab SHALL present nudges from the last 7 days, grouped under day headers with the newest day first. Each group SHALL be introduced by a header labeled `TODAY`, `YESTERDAY`, or the local date (e.g. `MON · AUG 18`) for older days, and the nudges under a header SHALL be those emitted on that local day, newest first. New nudges emitted while the panel is open SHALL appear under the correct day group without a reload.

#### Scenario: Nudges are grouped by day

- **WHEN** the panel opens with nudges emitted across several days within the last week
- **THEN** the Nudges tab shows a `TODAY` header above today's nudges, a `YESTERDAY` header above yesterday's, and a dated header above each older day, newest day first

#### Scenario: A new nudge joins today's group live

- **WHEN** a scenario emits a nudge while the panel is open
- **THEN** it appears at the top of the `TODAY` group without reopening the panel

#### Scenario: Only the last 7 days are loaded

- **WHEN** the Nudges tab loads
- **THEN** it queries nudges emitted at or after 7 days ago and renders those, rather than a flat fixed-count list

### Requirement: Nudges tab scenario filter

The Nudges tab SHALL provide a single-select filter that isolates one scenario's nudges. The filter SHALL offer `All` plus one option per scenario, default to `All`, and re-group the visible nudges by day when the selection changes.

#### Scenario: Filtering to one scenario

- **WHEN** the user selects the Achievements filter
- **THEN** the list shows only Achievements nudges, still grouped under day headers, and empty days disappear

#### Scenario: Returning to all scenarios

- **WHEN** the user selects `All`
- **THEN** the list shows every scenario's nudges again, grouped by day

#### Scenario: The filter is single-select

- **WHEN** the user selects a scenario chip while another is active
- **THEN** the newly selected chip becomes the only active one
