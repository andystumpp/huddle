## Why

Nudge bodies contain URLs — the Efficiency scenario cites sources by URL, and other scenarios mention links — but the `NudgeCard` renders the body as plain text (`BodyText.Text = Nudge.Body`), so the user can see a link but can't click it. It should be clickable and open the browser.

## What Changes

- The `NudgeCard` body renders URLs as clickable links that open the user's default browser, instead of flat text.
- Two tiers of detection:
  - **Explicit URLs** (`http://…`, `https://…`) are always linkified.
  - **Bare domains and `www.…`** are linkified only when the host's final label is a recognized web TLD (a curated allowlist), and the link is opened over `https`. The allowlist keeps dev-heavy bodies from mis-linking code tokens like `profile.md`, `Huddle.App`, `ScenarioPromptHelpers.cs`, or `2.1.3`.
- Trailing sentence punctuation (`.`, `)`, `,`, …) is trimmed off a match so it is not swallowed into the link.
- Title rendering, the copy control (still copies the full body text verbatim), and text selection are unchanged. Plain bodies with no URL render exactly as before.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `nudges`: the *Nudge card* requirement now specifies that URLs in the body render as clickable links opening the default browser.

## Impact

- `src/Huddle.App/Controls/BodyTextLinkifier.cs` — new; splits body text into `Run`/`Hyperlink` inlines.
- `src/Huddle.App/Controls/NudgeCard.xaml.cs` — `Apply()` builds `BodyText.Inlines` instead of assigning `BodyText.Text`.
- No XAML change (the existing `BodyText` `TextBlock` hosts inlines as-is); no storage or scenario changes.
