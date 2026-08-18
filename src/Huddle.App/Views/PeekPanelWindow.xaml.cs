using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using WinRT.Interop;
using Huddle.Capture;
using Huddle.Models;
using Huddle.Scenarios;
using Huddle.Storage;
using Huddle.Vision;

namespace Huddle.Views;

public sealed partial class PeekPanelWindow : Window
{
    private const int PanelWidth = 384;
    private const int RightGap = 12;
    private const int TopGap = 12;
    private const int BottomGap = 12;
    private const int MinPanelHeight = 320;
    private const int MaxVisibleMoments = 20;
    private const int MaxVisibleNudges = 20;

    // Slide / hover constants.
    private const int HideDelayMs = 700;        // cursor-off grace before sliding out
    private const int SlideDurationMs = 220;
    private const int SlideTickMs = 16;
    private const int HoverGrowPx = 8;          // extra pixels around the tab that still trigger show
    private const int ReadGraceMs = 3000;       // panel must stay open this long before unread → 0

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly TickScheduler _scheduler = new();
    private readonly ObservableCollection<Moment> _moments = new();
    private readonly ObservableCollection<Nudge> _nudges = new();

    private PeekTabWindow? _tab;

    private Storyboard? _pulseStoryboard;
    private DispatcherTimer? _statusTimer;
    private bool _pausedByLock;
    private SessionLockWatcher? _lockWatcher;

    // Cached panel geometry (set by PositionPanel).
    private int _panelY;
    private int _panelHeightPx;
    private int _panelWidthPx;
    private int _visibleX;
    private int _hiddenX;
    private int _workAreaRight;

    // Cached tab-window rect (set by PositionTab).
    private int _tabX, _tabY, _tabW, _tabH;
    private uint _lastDpi = 96;

    // Slide / hover state.
    private DispatcherTimer? _slideTimer;
    private DispatcherTimer? _hoverTimer;
    private DateTime _slideStartUtc;
    private int _slideFromX;
    private int _slideToX;
    private bool _isVisible = true;
    private DateTime? _leftPanelAtUtc;

    // Unread tracking — the chip shows nudges since the last "read."
    private int _unreadNudges;
    private bool _panelSeenForWhile;
    private DispatcherTimer? _readTimer;

    public PeekPanelWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        Title = "Huddle";
        _appWindow.Title = "Huddle";

        // Set the taskbar / titlebar icon explicitly — an unpackaged WinUI window
        // otherwise falls back to a generic icon even with <ApplicationIcon> set.
        try
        {
            _appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        }
        catch { /* icon is cosmetic; never block startup on it */ }

        ConfigureChrome();
        TrySetAcrylicBackdrop();

        if (Content is FrameworkElement fe)
        {
            fe.Loaded += OnContentLoaded;
        }

        Closed += OnWindowClosed;
    }

    private async void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        StartWatchDotPulse();
        UpdateStatus();
        UpdateLookBar();

        // Tabs default to Activity.
        TabActivity.IsChecked = true;
        TabNudges.IsChecked = false;
        UpdateTabSurface();
        CountNudges.Text = "0";

        // Bind the Activity / Nudges tabs to their live collections.
        MomentsRepeater.ItemsSource = _moments;
        NudgesRepeater.ItemsSource = _nudges;

        try
        {
            await Database.InitializeAsync();
            var recentMoments = await MomentStore.RecentAsync(MaxVisibleMoments);
            foreach (var m in recentMoments) _moments.Add(m);
            UpdateObservationCount();

            var recentNudges = await NudgeStore.RecentAsync(MaxVisibleNudges);
            foreach (var n in recentNudges) _nudges.Add(n);
            UpdateNudgesSurface();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] store init / load failed: {ex}");
        }

        // Drive the status-line countdown every second. While lock-paused,
        // also verify the real lock state so a missed unlock message
        // self-heals instead of leaving the panel paused forever.
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) =>
        {
            if (_pausedByLock && !SessionLockWatcher.IsSessionLocked())
            {
                ResumeFromLock();
            }
            UpdateStatus();
            UpdateLookBar();
        };
        _statusTimer.Start();

        // Drive the once-a-minute clock that refreshes every card's relative
        // timestamp ("3min ago") in place while the panel is alive.
        Time.RelativeTime.Start();

        // Start the real capture tick (fires once immediately, then every 180 s).
        _scheduler.Tick += OnSchedulerTick;
        _scheduler.Start();

        // Listen for workstation lock / unlock so we can auto-pause and
        // auto-resume. WTS messages arrive on this window's own thread, so the
        // handlers touch scheduler/UI state directly.
        _lockWatcher = new SessionLockWatcher(_hwnd);
        _lockWatcher.Locked += (_, _) => PauseForLock();
        _lockWatcher.Unlocked += (_, _) => ResumeFromLock();

        // Spin up the peek-tab window (the count badge that lives at the right
        // edge whenever the panel is hidden). Created here so PositionPanel has
        // already cached the work-area rect.
        _tab = new PeekTabWindow();
        _tab.UpdateCount(_unreadNudges);
        PositionPanel(); // recompute now that _tab exists, so its rect is set
        // Show once (without activation) so the XAML visual tree is realized,
        // then immediately hide. Without this first paint, subsequent Show()
        // calls leave the chip empty (no text, no halo) on WinUI 3.
        _tab.PeekAppWindow.Show(activateWindow: false);
        _tab.PeekAppWindow.Hide();

        StartHoverWatch();
        // Panel starts visible — kick off the read-grace timer so the chip
        // doesn't pulse from the moment the user first hides the panel.
        StartReadTimer();
    }

    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        _lockWatcher?.Dispose();
        _lockWatcher = null;

        _hoverTimer?.Stop();
        _slideTimer?.Stop();
        _statusTimer?.Stop();
        _readTimer?.Stop();
        Time.RelativeTime.Stop();

        _tab?.Close();
        _tab = null;
    }

    private void PauseForLock()
    {
        // Pause if currently watching; ignore otherwise (already paused —
        // a lock never overrides the user's own pause).
        if (_scheduler.IsPaused) return;

        _scheduler.Pause();
        _pausedByLock = true;
        PauseIcon.Visibility = Visibility.Collapsed;
        PlayIcon.Visibility = Visibility.Visible;
        _pulseStoryboard?.Stop();
        UpdateStatus();
        UpdateLookBar();
    }

    private void ResumeFromLock()
    {
        // Resume only if we were the ones who paused (user pause wins).
        if (!_scheduler.IsPaused || !_pausedByLock) return;

        _scheduler.Resume();
        _pausedByLock = false;
        PauseIcon.Visibility = Visibility.Visible;
        PlayIcon.Visibility = Visibility.Collapsed;
        _pulseStoryboard?.Begin();
        UpdateStatus();
        UpdateLookBar();
    }

    public void ShowPanel()
    {
        PositionPanel();
        SetTopmost(true);
        _appWindow.Show(activateWindow: true);
        Activate();
    }

    // --- chrome / backdrop -----------------------------------------------

    private void ConfigureChrome()
    {
        ExtendsContentIntoTitleBar = true;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Round the window surface itself (acrylic included) — borderless
        // windows lose Win11's default rounding, so request it explicitly.
        int cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
    }

    private void TrySetAcrylicBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        else if (Content is FrameworkElement fe)
        {
            fe.RequestedTheme = ElementTheme.Dark;
            if (RootClip != null)
            {
                RootClip.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xB8, 0x1C, 0x1E, 0x2A));
            }
        }
    }

    // --- tick / status / look-bar ---------------------------------------

    private void UpdateStatus()
    {
        if (_scheduler.IsPaused)
        {
            StatusText.Text = _pausedByLock
                ? "Paused · screen locked"
                : "Paused · not watching";
            WatchDot.Visibility = Visibility.Collapsed;
            WatchDotHalo.Visibility = Visibility.Collapsed;
        }
        else
        {
            int min = _scheduler.SecondsRemaining / 60;
            int sec = _scheduler.SecondsRemaining % 60;
            StatusText.Text = $"Watching · next look in {min}:{sec:D2}";
            WatchDot.Visibility = Visibility.Visible;
            WatchDotHalo.Visibility = Visibility.Visible;
        }
    }

    private void UpdateLookBar()
    {
        if (_scheduler.IsPaused)
        {
            LookBarScale.ScaleX = 0;
            return;
        }
        double progress = 1.0 - (double)_scheduler.SecondsRemaining / TickScheduler.PeriodSeconds;
        LookBarScale.ScaleX = Math.Clamp(progress, 0, 1);
    }

    private void StartWatchDotPulse()
    {
        // 2.6s pulse — halo scales 1→3 while fading.
        var scaleX = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(scaleX, WatchDotHaloScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1.0 });
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1820)), Value = 3.0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2600)), Value = 3.0 });

        var scaleY = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(scaleY, WatchDotHaloScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1.0 });
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1820)), Value = 3.0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2600)), Value = 3.0 });

        var opacity = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(opacity, WatchDotHalo);
        Storyboard.SetTargetProperty(opacity, "Opacity");
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0.55 });
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1820)), Value = 0.0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2600)), Value = 0.0 });

        _pulseStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(2600),
        };
        _pulseStoryboard.Children.Add(scaleX);
        _pulseStoryboard.Children.Add(scaleY);
        _pulseStoryboard.Children.Add(opacity);
        _pulseStoryboard.Begin();
    }

    // --- capture orchestration ------------------------------------------

    private const int TrailMoments = 6;

    private async void OnSchedulerTick(object? sender, EventArgs e)
    {
        // Level-triggered guard: the WM_WTSSESSION_CHANGE lock message can be
        // missed, so verify the real state before every capture. A missed
        // lock then costs one skipped tick, not a night of lock-screen calls.
        if (SessionLockWatcher.IsSessionLocked())
        {
            PauseForLock();
            return;
        }

        try
        {
            // Pull the trail BEFORE persisting the new moment so the model
            // never sees its own about-to-be-written summary.
            var recent = await MomentStore.RecentAsync(TrailMoments);

            var foreground = ForegroundContext.Read();
            byte[] jpeg = await ScreenCapture.CaptureAsJpegAsync();
            string summary = await MomentExtractor.ExtractAsync(jpeg, foreground, recent);

            var moment = new Moment(
                UlidGenerator.Generate(),
                DateTimeOffset.UtcNow,
                foreground.App,
                foreground.WindowTitle,
                summary);

            await MomentStore.AddAsync(moment);

            _moments.Insert(0, moment);
            while (_moments.Count > MaxVisibleMoments)
            {
                _moments.RemoveAt(_moments.Count - 1);
            }
            UpdateObservationCount();

            await RunScenariosAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] tick failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task RunScenariosAsync()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var scenario in ScenarioRegistry.All)
        {
            if (!scenario.IsDue(now)) continue;

            var trail = await MomentStore.RecentAsync(scenario.TrailSize);
            var priorNudges = await NudgeStore.RecentByScenarioAsync(scenario.Key, scenario.PriorNudgesSize);
            var result = await scenario.RunAsync(trail, priorNudges);
            if (result.Nudge is null) continue;

            await NudgeStore.AddAsync(result.Nudge);
            _nudges.Insert(0, result.Nudge);
            while (_nudges.Count > MaxVisibleNudges)
            {
                _nudges.RemoveAt(_nudges.Count - 1);
            }
            if (!_panelSeenForWhile) _unreadNudges++;
            UpdateNudgesSurface();
        }
    }

    private void UpdateNudgesSurface()
    {
        bool any = _nudges.Count > 0;
        NudgesEmptyState.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        NudgesScroll.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        CountNudges.Text = _nudges.Count.ToString();
        NudgeCountText.Text = _nudges.Count.ToString();
        _tab?.UpdateCount(_unreadNudges);
    }

    private async void OnRunScenariosNowClick(object sender, RoutedEventArgs e)
    {
        RunNowBtn.IsEnabled = false;
        RunNowBtn.Opacity = 0.4;
        ShowRunNowStatus("Running…");

        try
        {
            int emitted = 0;
            int silent = 0;
            string? firstReason = null;

            foreach (var scenario in ScenarioRegistry.All)
            {
                var trail = await MomentStore.RecentAsync(scenario.TrailSize);
                // Manual runs pass no prior nudges so dedup never suppresses
                // output — "Run now" is for eval, and a forced result beats
                // silence. Scheduled runs still dedup (see RunScenariosAsync).
                var result = await scenario.RunAsync(trail, Array.Empty<Nudge>());

                if (result.Nudge is not null)
                {
                    await NudgeStore.AddAsync(result.Nudge);
                    _nudges.Insert(0, result.Nudge);
                    while (_nudges.Count > MaxVisibleNudges)
                    {
                        _nudges.RemoveAt(_nudges.Count - 1);
                    }
                    if (!_panelSeenForWhile) _unreadNudges++;
                    emitted++;
                }
                else
                {
                    silent++;
                    if (firstReason is null && !string.IsNullOrWhiteSpace(result.SilentReason))
                    {
                        firstReason = result.SilentReason;
                    }
                }
            }

            UpdateNudgesSurface();

            if (emitted > 0)
            {
                ShowRunNowStatus($"Run complete: {emitted} emitted, {silent} silent");
            }
            else if (firstReason is not null)
            {
                ShowRunNowStatus($"Silent: {firstReason}");
            }
            else
            {
                ShowRunNowStatus("Scenario stayed silent");
            }
        }
        catch (Exception ex)
        {
            ShowRunNowStatus($"Error: {ex.Message}");
            Debug.WriteLine($"[Huddle] RunNow failed: {ex}");
        }
        finally
        {
            RunNowBtn.IsEnabled = true;
            RunNowBtn.Opacity = 1.0;
        }
    }

    private void ShowRunNowStatus(string text)
    {
        RunNowStatusText.Text = text;
        RunNowStatusText.Visibility = Visibility.Visible;
    }

    private void UpdateObservationCount()
    {
        ObservationCountText.Text = _moments.Count.ToString();
        CountActivity.Text = _moments.Count.ToString();
    }

    // --- pause / tab handlers --------------------------------------------

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        // Manual toggle always overrides auto-pause state.
        _pausedByLock = false;
        if (_scheduler.IsPaused)
        {
            _scheduler.Resume();
            PauseIcon.Visibility = Visibility.Visible;
            PlayIcon.Visibility = Visibility.Collapsed;
            _pulseStoryboard?.Begin();
        }
        else
        {
            _scheduler.Pause();
            PauseIcon.Visibility = Visibility.Collapsed;
            PlayIcon.Visibility = Visibility.Visible;
            _pulseStoryboard?.Stop();
        }
        UpdateStatus();
        UpdateLookBar();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;
        TabNudges.IsChecked = (clicked == TabNudges);
        TabActivity.IsChecked = (clicked == TabActivity);
        UpdateTabSurface();
    }

    private void UpdateTabSurface()
    {
        var nudgesSelected = TabNudges.IsChecked == true;
        NudgesContent.Visibility = nudgesSelected ? Visibility.Visible : Visibility.Collapsed;
        ActivityContent.Visibility = nudgesSelected ? Visibility.Collapsed : Visibility.Visible;
    }

    // --- positioning -----------------------------------------------------

    private void PositionPanel()
    {
        if (!TryGetPrimaryWorkArea(out var workArea, out var dpi))
        {
            return;
        }
        _lastDpi = dpi;

        var widthPx = ScaleToPx(PanelWidth, dpi);
        var rightGapPx = ScaleToPx(RightGap, dpi);
        var topGapPx = ScaleToPx(TopGap, dpi);
        var bottomGapPx = ScaleToPx(BottomGap, dpi);
        var minHeightPx = ScaleToPx(MinPanelHeight, dpi);

        var heightPx = workArea.Height - topGapPx - bottomGapPx;
        if (heightPx < minHeightPx) heightPx = minHeightPx;

        _panelWidthPx = widthPx;
        _panelHeightPx = heightPx;
        _panelY = workArea.Y + topGapPx;
        _workAreaRight = workArea.X + workArea.Width;
        _visibleX = _workAreaRight - widthPx - rightGapPx;
        // Hidden state: panel parked fully off the right edge of the work area.
        _hiddenX = _workAreaRight;

        var startX = _isVisible ? _visibleX : _hiddenX;
        _appWindow.MoveAndResize(new RectInt32(startX, _panelY, widthPx, heightPx));

        LookBarClip.Rect = new Windows.Foundation.Rect(0, 0, PanelWidth, 2);

        // Tab window — anchored so its left 28 dip sit on screen at the right
        // edge, vertically centered. The OS clamps the actual window wider
        // than requested (~133px min track width); the excess hangs off-screen
        // and the chip content is left-anchored in XAML, so that's harmless.
        var tabW = ScaleToPx(PeekTabWindow.VisibleWidthDip, dpi);
        var tabH = ScaleToPx(PeekTabWindow.HeightDip, dpi);
        _tabW = tabW;
        _tabH = tabH;
        _tabX = _workAreaRight - tabW;
        _tabY = workArea.Y + (workArea.Height - tabH) / 2;
        _tab?.PeekAppWindow.MoveAndResize(new RectInt32(_tabX, _tabY, tabW, tabH));
        _tab?.ApplyRoundedRegion(tabW, tabH, ScaleToPx(PeekTabWindow.CornerRadiusDip, dpi));
    }

    // --- slide / hover --------------------------------------------------

    private void StartHoverWatch()
    {
        if (_hoverTimer is not null) return;
        _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _hoverTimer.Tick += (_, _) => UpdateHoverState();
        _hoverTimer.Start();
    }

    private void UpdateHoverState()
    {
        if (_panelHeightPx == 0) return; // PositionPanel hasn't run yet
        if (!GetCursorPos(out var pt)) return;

        bool inPanelYBand = pt.Y >= _panelY && pt.Y <= _panelY + _panelHeightPx;

        if (_isVisible)
        {
            // Auto-hide once the cursor sits outside the panel's visible bounds
            // (using _visibleX so an in-progress slide-in doesn't self-cancel).
            bool overPanel = inPanelYBand && pt.X >= _visibleX && pt.X <= _workAreaRight;
            if (overPanel)
            {
                _leftPanelAtUtc = null;
            }
            else
            {
                _leftPanelAtUtc ??= DateTime.UtcNow;
                if ((DateTime.UtcNow - _leftPanelAtUtc.Value).TotalMilliseconds >= HideDelayMs)
                {
                    Slide(toVisible: false);
                }
            }
        }
        else
        {
            // Show when the cursor enters the tab window's rect (grown a bit
            // for forgiveness, especially on the screen-edge side).
            bool overTab = pt.X >= _tabX - HoverGrowPx
                        && pt.X <= _workAreaRight
                        && pt.Y >= _tabY - HoverGrowPx
                        && pt.Y <= _tabY + _tabH + HoverGrowPx;
            if (overTab)
            {
                Slide(toVisible: true);
            }
        }
    }

    private void Slide(bool toVisible)
    {
        var targetX = toVisible ? _visibleX : _hiddenX;
        var currentX = _appWindow.Position.X;
        _isVisible = toVisible;
        _leftPanelAtUtc = null;

        // Hide the tab window immediately on slide-in so it's gone before the
        // panel arrives. (On slide-out we surface it at the end of the animation.)
        if (toVisible)
        {
            _tab?.PeekAppWindow.Hide();
            StartReadTimer();
        }
        else
        {
            _readTimer?.Stop();
            _panelSeenForWhile = false;
        }

        if (currentX == targetX)
        {
            if (!toVisible) ShowTab();
            return;
        }

        _slideFromX = currentX;
        _slideToX = targetX;
        _slideStartUtc = DateTime.UtcNow;

        if (_slideTimer is null)
        {
            _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SlideTickMs) };
            _slideTimer.Tick += (_, _) => OnSlideTick();
        }
        _slideTimer.Start();
    }

    private void OnSlideTick()
    {
        var elapsed = (DateTime.UtcNow - _slideStartUtc).TotalMilliseconds;
        var t = Math.Clamp(elapsed / SlideDurationMs, 0.0, 1.0);
        // ease-out cubic
        var eased = 1.0 - Math.Pow(1.0 - t, 3);
        var x = (int)Math.Round(_slideFromX + (_slideToX - _slideFromX) * eased);

        _appWindow.Move(new PointInt32(x, _panelY));

        if (t >= 1.0)
        {
            _slideTimer!.Stop();
            if (!_isVisible)
            {
                ShowTab();
            }
        }
    }

    private void ShowTab()
    {
        if (_tab is null || _tabW == 0) return;
        _tab.PeekAppWindow.MoveAndResize(new RectInt32(_tabX, _tabY, _tabW, _tabH));
        _tab.PeekAppWindow.Show(activateWindow: false);
        // Keep the tab above other windows even when the panel isn't topmost.
        SetWindowPos(_tab.Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void StartReadTimer()
    {
        if (_readTimer is null)
        {
            _readTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ReadGraceMs) };
            _readTimer.Tick += (_, _) => OnReadGraceElapsed();
        }
        _readTimer.Stop();
        _readTimer.Start();
    }

    private void OnReadGraceElapsed()
    {
        _readTimer?.Stop();
        if (!_isVisible) return; // belt-and-braces: panel slid out during the grace
        _panelSeenForWhile = true;
        if (_unreadNudges != 0)
        {
            _unreadNudges = 0;
            _tab?.UpdateCount(0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    private static int ScaleToPx(int designPx, uint dpi)
    {
        return (int)Math.Round(designPx * dpi / 96.0);
    }

    private void SetTopmost(bool topmost)
    {
        var insertAfter = topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private bool TryGetPrimaryWorkArea(out RectInt32 workArea, out uint dpi)
    {
        workArea = default;
        dpi = 96;

        var monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTOPRIMARY);
        if (monitor == IntPtr.Zero) return false;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref mi)) return false;

        workArea = new RectInt32(
            mi.rcWork.Left,
            mi.rcWork.Top,
            mi.rcWork.Right - mi.rcWork.Left,
            mi.rcWork.Bottom - mi.rcWork.Top);

        if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0)
        {
            dpi = dpiX;
        }
        return true;
    }

    // --- P/Invoke ---

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
