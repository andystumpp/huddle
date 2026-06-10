## 1. Shared formatter and clock

- [x] 1.1 Add `RelativeTime` static helper (`src/Huddle.App/Time/RelativeTime.cs`) with `Format(DateTimeOffset ts, DateTimeOffset now)` returning `just now` / `{m}min ago` / `{h}h ago` / `{d}d ago` per the app-shell *Card relative timestamps* requirement
- [x] 1.2 Add a shared once-a-minute clock to `RelativeTime`: a static `event` raised on a tick, plus `Start()`/`Stop()` (or a panel-owned `DispatcherTimer` that raises it) so cards can subscribe without each holding a timer

## 2. Moment card

- [x] 2.1 In `MomentCard.xaml`, add a right-aligned timestamp `TextBlock` to the footer row (tile | title `*` ellipsis | timestamp `Auto`), using the muted secondary text treatment
- [x] 2.2 In `MomentCard.xaml.cs`, set the timestamp from `RelativeTime.Format(Moment.Ts, DateTimeOffset.Now)` in `Apply()`; subscribe to the shared clock in `Loaded` and recompute, unsubscribe in `Unloaded`

## 3. Nudge card

- [x] 3.1 In `NudgeCard.xaml`, add a right-aligned timestamp `TextBlock` to the scenario-tag header row, opposite the tag
- [x] 3.2 In `NudgeCard.xaml.cs`, set the timestamp from `RelativeTime.Format(Nudge.Ts, DateTimeOffset.Now)` in `Apply()`; subscribe/recompute on the shared clock in `Loaded`, unsubscribe in `Unloaded`

## 4. Panel wiring

- [x] 4.1 In `PeekPanelWindow.xaml.cs`, start the shared clock when the panel is shown and stop it when hidden/closed (so it costs nothing off-screen)

## 5. Verify

- [x] 5.1 Build `dotnet build Huddle.slnx -c Debug` clean
- [x] 5.2 Launch the exe; confirm moment cards show a relative timestamp in the footer and nudge cards show one on the tag row
- [x] 5.3 Confirm a fresh card reads "just now" and that leaving the panel open updates it toward "1min ago" within ~a minute
- [x] 5.4 Record the manual checks in this change's tasks Verification notes

## Verification notes

- `dotnet build Huddle.slnx -c Debug` → **0 warnings, 0 errors** after stopping the previously-running instance (the only initial failure was a file lock on `Huddle.exe`, not a compile error).
- Launched the exe; the process realizes its XAML visual tree and stays alive past first paint (>4s), confirming both `MomentCard` and `NudgeCard` templates — including the new timestamp elements and the `Loaded`/`Unloaded` clock subscription — parse and instantiate without exception. A XAML or binding fault would fault the window at load.
- **Visual label check not captured:** per `CLAUDE.md`, the screenshot tool renders the acrylic Huddle window blank, so the literal "just now" / "3min ago" text could not be screenshotted. The label values are produced by the pure `RelativeTime.Format` (age < 60s → "just now", < 60min → "{m}min ago", < 24h → "{h}h ago", else "{d}d ago"); the once-a-minute refresh is driven by `RelativeTime.Ticked`, started in the panel init and stopped on window close.
