## MODIFIED Requirements

### Requirement: Nudge card

Each nudge SHALL be rendered as a `NudgeCard` containing, top to bottom: a header row with a scenario tag (colored dot + the scenario's `DisplayName`, looked up by `nudge.Scenario` via `ScenarioRegistry.GetByKey`) on the left and a relative timestamp derived from `nudge.ts` (per the app-shell *Card relative timestamps* requirement) on the right, the nudge title (semibold, primary text color), and the nudge body (regular weight, secondary text color, wrapping). Within the body, URLs SHALL render as clickable links that open the user's default browser; explicit `http`/`https` URLs are linked as-is, and a bare domain or `www.` host is linked over `https` when its final label is a recognized web TLD. If the registry returns no match for `nudge.Scenario`, the tag SHALL fall back to the upper-cased scenario key and the default violet dot. The card SHALL NOT show any action affordances beyond the existing star and copy controls and the in-body links in this change.

#### Scenario: Card pulls display from the registry

- **WHEN** a nudge card renders with `nudge.Scenario = "achievements"`
- **THEN** the tag reads `ACHIEVEMENTS` and the colored dot uses the `AccentColorHex` registered by the Achievements scenario

#### Scenario: Card falls back when scenario is unknown

- **WHEN** a nudge card renders with a `nudge.Scenario` that does not match any registered scenario
- **THEN** the tag reads the upper-cased scenario key and the dot uses the default violet color

#### Scenario: Card shows a relative timestamp

- **WHEN** a nudge card renders with a `nudge.ts` 2 hours before the current time
- **THEN** the header row shows the relative timestamp "2h ago" to the right of the scenario tag

#### Scenario: A URL in the body is clickable

- **WHEN** a nudge body contains `https://example.com/x` or a bare `example.com` whose final label is a recognized web TLD
- **THEN** that text renders as a clickable link that opens the user's default browser (bare hosts opened over `https`), while the surrounding text stays plain and any trailing sentence punctuation is left outside the link

#### Scenario: Code-like tokens are not linked

- **WHEN** a nudge body contains tokens such as `profile.md`, `Huddle.App`, or `2.1.3` whose final label is not a recognized web TLD
- **THEN** those tokens render as plain text, not links
