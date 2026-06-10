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
using Huddle.Storage;
using Huddle.Vision;

namespace Huddle.Views;

public sealed partial class PeekPanelWindow : Window
{
    private const int PanelWidth = 384;
    private const int RightGap = 12;
    private const int BottomGap = 12;
    private const int HeightHeadroom = 84;
    private const int DesiredHeight = 460;
    private const int MaxVisibleMoments = 20;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly TickScheduler _scheduler = new();
    private readonly ObservableCollection<Moment> _moments = new();

    private Storyboard? _pulseStoryboard;
    private DispatcherTimer? _statusTimer;

    public PeekPanelWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        Title = "Huddle";
        _appWindow.Title = "Huddle";

        ConfigureChrome();
        TrySetAcrylicBackdrop();

        if (Content is FrameworkElement fe)
        {
            fe.Loaded += OnContentLoaded;
        }
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

        // Bind the Activity tab to the live moments collection.
        MomentsRepeater.ItemsSource = _moments;

        try
        {
            await Database.InitializeAsync();
            var recent = await MomentStore.RecentAsync(MaxVisibleMoments);
            foreach (var m in recent) _moments.Add(m);
            UpdateObservationCount();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] store init / load failed: {ex}");
        }

        // Drive the status-line countdown every second.
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => { UpdateStatus(); UpdateLookBar(); };
        _statusTimer.Start();

        // Start the real capture tick (fires once immediately, then every 180 s).
        _scheduler.Tick += OnSchedulerTick;
        _scheduler.Start();
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
            StatusText.Text = "Paused · not watching";
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

    private async void OnSchedulerTick(object? sender, EventArgs e)
    {
        try
        {
            var foreground = ForegroundContext.Read();
            byte[] jpeg = await ScreenCapture.CaptureAsJpegAsync();
            string summary = await MomentExtractor.ExtractAsync(jpeg, foreground);

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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Huddle] tick failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void UpdateObservationCount()
    {
        ObservationCountText.Text = _moments.Count.ToString();
        CountActivity.Text = _moments.Count.ToString();
    }

    // --- pause / tab handlers --------------------------------------------

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
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

        var widthPx = ScaleToPx(PanelWidth, dpi);
        var rightGapPx = ScaleToPx(RightGap, dpi);
        var bottomGapPx = ScaleToPx(BottomGap, dpi);
        var headroomPx = ScaleToPx(HeightHeadroom, dpi);

        var maxHeight = workArea.Height - headroomPx;
        var desiredHeight = Math.Min(maxHeight, ScaleToPx(DesiredHeight, dpi));
        if (desiredHeight < ScaleToPx(80, dpi)) desiredHeight = ScaleToPx(80, dpi);

        var x = workArea.X + workArea.Width - widthPx - rightGapPx;
        var y = workArea.Y + workArea.Height - desiredHeight - bottomGapPx;

        _appWindow.MoveAndResize(new RectInt32(x, y, widthPx, desiredHeight));

        LookBarClip.Rect = new Windows.Foundation.Rect(0, 0, PanelWidth, 2);
    }

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
}
