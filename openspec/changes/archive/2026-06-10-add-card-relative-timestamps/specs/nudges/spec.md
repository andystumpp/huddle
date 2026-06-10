## MODIFIED Requirements

### Requirement: Nudge card

Each nudge SHALL be rendered as a `NudgeCard` containing, top to bottom: a header row with a scenario tag (colored dot + the scenario's `DisplayName`, looked up by `nudge.Scenario` via `ScenarioRegistry.GetByKey`) on the left and a relative timestamp derived from `nudge.ts` (per the app-shell *Card relative timestamps* requirement) on the right, the nudge title (semibold, primary text color), and the nudge body (regular weight, secondary text color, wrapping). If the registry returns no match for `nudge.Scenario`, the tag SHALL fall back to the upper-cased scenario key and the default violet dot. The card SHALL NOT show any action affordances beyond the existing star and copy controls in this change.

#### Scenario: Card pulls display from the registry

- **WHEN** a nudge card renders with `nudge.Scenario = "achievements"`
- **THEN** the tag reads `ACHIEVEMENTS` and the colored dot uses the `AccentColorHex` registered by the Achievements scenario

#### Scenario: Card falls back when scenario is unknown

- **WHEN** a nudge card renders with a `nudge.Scenario` that does not match any registered scenario
- **THEN** the tag reads the upper-cased scenario key and the dot uses the default violet color

#### Scenario: Card shows a relative timestamp

- **WHEN** a nudge card renders with a `nudge.ts` 2 hours before the current time
- **THEN** the header row shows the relative timestamp "2h ago" to the right of the scenario tag
