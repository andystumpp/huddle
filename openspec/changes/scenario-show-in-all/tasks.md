## 1. Config field

- [x] 1.1 `ScenarioConfig.cs`: add `public bool ShowInAll { get; init; } = true;`.
- [x] 1.2 `HuddleConfig.cs`: add a `Bool(name, fallback)` parse helper and set `ShowInAll = Bool("showInAll", true)` (reuse it for `webSearch` too). Absent or `true` → true; only explicit `false` disables.

## 2. Expose on the scenario

- [x] 2.1 `Scenario.cs`: add `public virtual bool ShowInAll => true;`.
- [x] 2.2 `ConfiguredScenario.cs`: `public override bool ShowInAll => _def.ShowInAll;`.

## 3. Apply the gate

- [x] 3.1 `PeekPanelWindow`: add `private static bool ShowsInAll(string key) => ScenarioRegistry.GetByKey(key)?.ShowInAll ?? true;`.
- [x] 3.2 `RebuildNudgeDisplay`: in the `All` branch (`_activeScenarioFilter is null`), skip nudges where `!ShowsInAll(n.Scenario)`. A specific-scenario filter is unchanged.
- [x] 3.3 Guard both `_unreadNudges++` sites (tick `RunScenariosAsync` and manual run) with `ShowsInAll(result.Nudge.Scenario)`.

## 4. Example config

- [x] 4.1 `huddle.config.example.json`: document `showInAll`; set `achievements` to `"showInAll": false`.

## 5. Verify

- [x] 5.1 `dotnet build Huddle.slnx -c Debug` clean.
- [x] 5.2 App launches and renders the existing Nudges list through the new `All`-branch gate without faulting (visual All-excludes / pill-shows check is manual — steps below).
- [ ] 5.3 Manual (user): a new Achievements nudge while the panel is hidden does not raise the chip's unread count; a `showInAll: true` scenario's nudge does.
- [x] 5.4 Record commands and outcomes in §Verification.

## Verification

Run on the real machine, 2026-08-24.

**5.1 — Build.** `dotnet build Huddle.slnx -c Debug` → `Build succeeded. 0 Error(s)`.

**5.2 — Render smoke.** Launched the freshly built exe; the Nudges tab rendered through the new `RebuildNudgeDisplay` `All`-branch gate and the process stayed alive (no fault). The transparent window is invisible to the screenshot tool, so the visual behavior is a manual check.

**Config parsing.** `showInAll` parses via the new `Bool("showInAll", true)` helper: absent or `true` → shown; only explicit `false` hides. `huddle.config.example.json` now sets `achievements` to `"showInAll": false`.

**5.2 / 5.3 — Manual behavior check (user).** The running config that matters is `%LOCALAPPDATA%\Huddle\huddle.config.json` — add `"showInAll": false` to the `achievements` scenario there and restart Huddle. Then:
- `All` view shows no Achievements cards; other scenarios still appear.
- Selecting the **Achievements** pill shows its nudges (still reachable).
- A new Achievements nudge while the panel is hidden leaves the chip's unread count and halo unchanged; a `showInAll: true` scenario's nudge still bumps it.
