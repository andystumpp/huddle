## 1. Strip tray code

- [x] 1.1 In `src/Huddle.App/App.xaml.cs`, remove the `TaskbarIcon` instantiation, the `MenuFlyout` setup, `OnTrayLeftClick`, and the `Quit()` helper
- [x] 1.2 Replace `OnLaunched` body with: construct `PeekPanelWindow`, hook `Closed` to call `Exit()`, call `ShowPanel()`
- [x] 1.3 Delete `src/Huddle.App/RelayCommand.cs`
- [x] 1.4 Remove the `H.NotifyIcon.WinUI` `PackageReference` from `src/Huddle.App/Huddle.App.csproj`

## 2. Make the window a normal taskbar window

- [x] 2.1 In `PeekPanelWindow.ConfigureChrome`, set `ExtendsContentIntoTitleBar = false`; on the `OverlappedPresenter`, leave `SetBorderAndTitleBar(true, true)` (or omit the override) so the native title bar renders
- [x] 2.2 Keep `IsResizable = false`, `IsMinimizable = false`, `IsMaximizable = false`, `IsAlwaysOnTop = true`
- [x] 2.3 Delete the `WS_EX_TOOLWINDOW` style application (and `GetWindowLong` / `SetWindowLong` P/Invokes if unused after)

## 3. Drop auto-hide and bookkeeping

- [x] 3.1 Remove `IsPanelVisible` and `LastDeactivatedAt` properties on `PeekPanelWindow`
- [x] 3.2 Remove the `Activated += OnActivated` subscription and the `OnActivated` handler
- [x] 3.3 Remove the `KeyDown += OnContentKeyDown` subscription and the `OnContentKeyDown` handler (and the `Windows.System` using if it becomes unused)
- [x] 3.4 Remove the `HidePanel()` method (no longer called)
- [x] 3.5 In the constructor, drop `_appWindow.Hide()` so the window starts in its default shown state; let `ShowPanel()` from `OnLaunched` drive the final position + activation

## 4. Verification

- [x] 4.1 `dotnet build Huddle.slnx -c Debug` succeeds with 0 warnings, 0 errors
- [x] 4.2 Launch the app — the panel appears at the bottom-right, 384 px wide, with the brand block; a Huddle taskbar entry appears
- [x] 4.3 Activate another window — the panel stays visible (no auto-hide)
- [x] 4.4 Click the panel's close button — the app exits and the taskbar entry disappears
- [x] 4.5 Confirm there is no Huddle icon in the system tray
