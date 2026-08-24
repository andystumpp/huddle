## 1. Linkifier helper

- [x] 1.1 Add `src/Huddle.App/Controls/BodyTextLinkifier.cs`: `static IReadOnlyList<Inline> BuildInlines(string body)`. Scan left to right; plain spans → `Run`, URL spans → `Hyperlink { NavigateUri }`. Tier 1: `https?://[^\s<>"]+`. Tier 2: `(www\.)?host(\.host)+(/path)?` where the host is lowercase and its final label is in a curated web-TLD allowlist (`com org net io dev ai co app xyz info news me tv gg so edu gov de uk ca fr eu`), opened over `https`. Trim trailing `.,;:!?)]}'"` off a match back to plain text. Validate each candidate with `Uri.TryCreate(..., UriKind.Absolute)` and scheme `http`/`https`; on failure emit a `Run`. A body with no URL returns a single `Run`.

## 2. Wire into the card

- [x] 2.1 In `NudgeCard.Apply()`, replace `BodyText.Text = Nudge.Body;` with `BodyText.Inlines.Clear();` then add each inline from `BodyTextLinkifier.BuildInlines(Nudge.Body)`. Stop assigning `BodyText.Text` anywhere.
- [x] 2.2 Give the `Hyperlink`s a foreground that fits the card's translucent palette (a light accent `#8AB4F8`), keeping the default hover underline. Copy, title, and `IsTextSelectionEnabled` stay unchanged.

## 3. Verify

- [x] 3.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 3.2 Parser check over samples (via a headless harness mirroring the exact regex/allowlist/trim, since `BuildInlines` returns WinUI `Inline`s that need the UI runtime). Record the observed inline breakdown.
- [x] 3.3 Live smoke: app launches and renders the existing Nudges list through the new inline path without faulting. The visual click-through is a manual check (the transparent window is invisible to the screenshot tool) — steps recorded below.
- [x] 3.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-23.

**3.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**3.2 — Parser.** Headless harness mirroring the exact matcher over samples:

| Input fragment | Result |
|---|---|
| `No links here at all.` | one plain run |
| `https://example.com/x for details.` | link `https://example.com/x`, rest plain |
| `Read example.com. Then stop.` | link `example.com` → `https://example.com/`, trailing `.` left plain |
| `Ref (https://foo.com) here.` | `(` plain, link `https://foo.com`, `)` trimmed back to plain |
| `www.anthropic.com!` | link → `https://www.anthropic.com/`, `!` plain |
| `github.com/andystumpp/huddle`, `bahn.de` | linked over `https` |
| `profile.md`, `Huddle.App`, `Microsoft.UI.Xaml`, `ScenarioPromptHelpers.cs`, `2.1.3` | all plain |
| `build.sh` | plain (`.sh` excluded) |

The harness caught a false positive on first run — `Huddle.App` linked because `.app` is a real TLD — fixed by also requiring bare hosts to be lowercase (PascalCase `Huddle.App` / `Microsoft.UI.Xaml` are code, not domains). Explicit `http(s)://` URLs are unaffected by the case rule.

**3.3 — Live render.** Launched the freshly built exe; the process rendered the existing Nudges tab through the new `BodyText.Inlines` path and stayed alive (no render-time fault). The transparent window is invisible to the screenshot tool, so the visual click-through is a manual check:

_Manual steps (user):_ open the Nudges tab; a body containing a URL (e.g. an Efficiency nudge's cited source) shows the URL in the light-accent link color; clicking it opens the default browser; a nudge with no URL looks identical to before; the Copy button still yields the full body text including the URL.

