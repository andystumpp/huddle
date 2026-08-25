## MODIFIED Requirements

### Requirement: Nudges tab scenario filter

The Nudges tab SHALL provide a single-select filter that isolates one scenario's nudges. The filter SHALL offer `All` plus one option per scenario, default to `All`, and re-group the visible nudges by day when the selection changes. The `All` view SHALL show every scenario's nudges except those from scenarios configured with `showInAll: false`; those nudges SHALL appear only under their own scenario filter. Every configured scenario SHALL keep its own filter option regardless of `showInAll`.

#### Scenario: Filtering to one scenario

- **WHEN** the user selects the Achievements filter
- **THEN** the list shows only Achievements nudges, still grouped under day headers, and empty days disappear

#### Scenario: Returning to all scenarios

- **WHEN** the user selects `All`
- **THEN** the list shows every scenario's nudges again, grouped by day, except those from `showInAll: false` scenarios

#### Scenario: The filter is single-select

- **WHEN** the user selects a scenario chip while another is active
- **THEN** the newly selected chip becomes the only active one

#### Scenario: A hidden scenario is still reachable by its pill

- **WHEN** a scenario is configured with `showInAll: false` and the user selects that scenario's filter
- **THEN** the list shows that scenario's nudges, even though they do not appear under `All`

### Requirement: Unread nudge count on the chip

The chip SHALL display the number of nudges that arrived while the panel was not "seen". A nudge increments the unread count when it is inserted and the panel has not been open for the read-grace period, unless the nudge's scenario is configured with `showInAll: false`, in which case it SHALL NOT change the unread count. The unread count SHALL reset to 0 — and the panel SHALL be marked seen — once the panel has stayed open for 3 seconds; sliding out before that preserves the count. While the unread count is greater than zero, the chip SHALL render a pulsing halo behind the number; at zero the halo is still.

#### Scenario: Nudges arriving while hidden increment the count

- **WHEN** the panel is hidden and a `showInAll` scenario emits two nudges
- **THEN** the chip shows "2" with a pulsing halo

#### Scenario: Count resets after the panel is open 3 seconds

- **WHEN** the panel slides in and stays open for 3 seconds
- **THEN** the unread count resets to 0 and the chip shows "0" with no pulse on the next slide-out

#### Scenario: A quick peek preserves the count

- **WHEN** the panel slides in and slides back out in under 3 seconds
- **THEN** the chip still shows the previous unread count

#### Scenario: A hidden scenario does not raise the count

- **WHEN** the panel is hidden and a `showInAll: false` scenario emits a nudge
- **THEN** the chip's unread count is unchanged and its halo stays still
