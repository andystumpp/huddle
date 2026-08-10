# Huddle — agent notes

A Windows desktop app that quietly watches the user's screen, sends frames to Claude, and surfaces small observations and suggestions in a docked peek panel. Vision in `product/outline.md`. Architecture in `architecture/0001-high-level-architecture.md`. Read both before non-trivial work.

## Stack

- WinUI 3 + .NET 10 + Windows App SDK **2.1.3** (1.6 errors on .NET 10 — don't downgrade).
- Unpackaged (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`).
- Anthropic C# SDK (`Anthropic` NuGet) for any Claude calls — never hand-write the HTTP. Default model: `Model.ClaudeOpus4_8` unless the design doc says otherwise. API key: `ANTHROPIC_API_KEY` env var.
- Solution is `Huddle.slnx` (the new XML format) at repo root; app project at `src/Huddle.App/`.

## Build / run

```powershell
dotnet build Huddle.slnx -c Debug
Start-Process "src\Huddle.App\bin\Debug\net10.0-windows10.0.26100.0\win-x64\Huddle.exe"
```

`dotnet run` picks the MSIX launch profile and fails — launch the exe directly. The screenshot tool can't see `huddle.exe` (masking renders blank for transparent/acrylic windows), so verify position via `GetWindowRect` from PowerShell. Manual checks belong in `tasks.md` §Verification, not in code.

## OpenSpec workflow

Every non-trivial change goes through OpenSpec: `/opsx:propose` → `/opsx:apply` → `/opsx:archive`. Iterate on the artifacts when scope shifts mid-flight rather than working around them.

- **Specs describe what something *is*, not what it isn't.** No "SHALL NOT show a scenario tag" scenarios — that's defensive and ages badly. Write the positive contract; future additions are new requirements.
- **One iteration = one PR = one merged change.** When the user says "iteration by iteration," strip fields out rather than carry them speculatively (we dropped scenario / last-seen / strength / nudge-count from `Pattern` for exactly this reason).
- **Lock the visual contract before storage.** Seeded in-memory data first, real persistence later. Surface the seed behind a single `static readonly` reference so swapping to a store touches one file.

## Design docs

Every `design.md` — and any design discussion in chat — leads with a Mermaid **sequence diagram**. The diagram is the spine of the design, not decoration; the prose hangs off it.

- **Divide the flow into labeled sections** — one per distinct phase of processing (use separate diagrams per phase, or `Note over` / `rect` bands within one). Name each section.
- **Every section states its contract.** Spell out the exact data crossing each boundary: the request/response types, their fields, invariants, and the empty/error cases. Name the real types (`ScenarioRequest`, `BackendResult`, `NudgeDraft`) — never "some data".
- **Within each section, describe the how.** Beneath the diagram, key prose to each section explaining the processing that turns its input contract into its output contract — the steps, the branch conditions, the transforms.

The bar: a reader can implement any one section from its contract + how alone, without reverse-engineering the others.

## Engineering principles

- **YAGNI.** Default to no. Add a field, abstraction, or framework when a current change needs it, not before. If we removed it last iteration, don't sneak it back this one. The skill notes in spec deltas (D2-style "deliberately omitted, likely back later") are the right format — name what's missing and why.
- **DRY** when reuse is *real* — `AppTile`, `PatternCard`, palette resources in `PeekPanelWindow.xaml` `<Grid.Resources>`. Don't preemptively extract for hypothetical reuse.
- **SOLID** at the level the codebase warrants:
  - Single-responsibility per file — already the norm. Keep it that way.
  - Don't introduce interfaces / DI / abstractions until there's a second implementation that needs them. WinUI app + Anthropic SDK don't need a service locator yet.
  - When something does grow a second variant (e.g. moments come from SQLite *and* the seed), that's the moment for an interface — not before.

