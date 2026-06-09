using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Generic;
using Windows.Graphics;
using WinRT.Interop;
using Huddle.Capture;
using Huddle.Models;
using Huddle.Vision;

namespace Huddle.Views;

public sealed partial class PeekPanelWindow : Window
{
    private const int PanelWidth = 384;
    private const int RightGap = 12;
    private const int BottomGap = 12;
    private const int HeightHeadroom = 84;
    private const int DesiredHeight = 460;

    // Demo cadence — short so the panel feels alive while we're iterating.
    // Real Huddle ticks every 3 minutes (ADR 0001).
    private const int TickSeconds = 18;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly DispatcherTimer _tickTimer;

    private int _secondsRemaining = TickSeconds;
    private bool _paused;
    private Storyboard? _pulseStoryboard;

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

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += OnTick;

        if (Content is FrameworkElement fe)
        {
            fe.Loaded += OnContentLoaded;
        }
    }

    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        StartWatchDotPulse();
        UpdateStatus();
        UpdateLookBar();
        _tickTimer.Start();

        // Wire up the tabs + counts + content.
        PatternsRepeater.ItemsSource = PatternSeed.All;
        CountActivity.Text = PatternSeed.All.Count.ToString();
        CountNudges.Text = "0";

        // Default selected tab: Activity.
        TabActivity.IsChecked = true;
        TabNudges.IsChecked = false;
        UpdateTabSurface();

        PatternCountText.Text = PatternSeed.All.Count.ToString();
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
            // Tuned to roughly match the prototype's `aurora` panel-bg:
            // rgba(28, 30, 42, 0.66) over a heavy blur+saturation, with the
            // aurora gradients carrying most of the chromatic character.
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

    // --- tick / look-bar -------------------------------------------------

    private void OnTick(object? sender, object e)
    {
        if (_paused) return;
        _secondsRemaining--;
        if (_secondsRemaining <= 0) _secondsRemaining = TickSeconds;
        UpdateStatus();
        UpdateLookBar();
    }

    private void UpdateStatus()
    {
        if (_paused)
        {
            StatusText.Text = "Paused · not watching";
            WatchDot.Visibility = Visibility.Collapsed;
            WatchDotHalo.Visibility = Visibility.Collapsed;
        }
        else
        {
            var min = _secondsRemaining / 60;
            var sec = _secondsRemaining % 60;
            StatusText.Text = $"Watching · next look in {min}:{sec:D2}";
            WatchDot.Visibility = Visibility.Visible;
            WatchDotHalo.Visibility = Visibility.Visible;
        }
    }

    private void UpdateLookBar()
    {
        if (_paused)
        {
            LookBarScale.ScaleX = 0;
            return;
        }
        var progress = 1.0 - (double)_secondsRemaining / TickSeconds;
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

    // --- pause / chip event handlers ------------------------------------

    private void OnPauseClick(object sender, RoutedEventArgs e)
    {
        _paused = !_paused;
        if (_paused)
        {
            PauseIcon.Visibility = Visibility.Collapsed;
            PlayIcon.Visibility = Visibility.Visible;
            _pulseStoryboard?.Stop();
        }
        else
        {
            PauseIcon.Visibility = Visibility.Visible;
            PlayIcon.Visibility = Visibility.Collapsed;
            _pulseStoryboard?.Begin();
            _secondsRemaining = TickSeconds; // restart the cycle on resume
        }
        UpdateStatus();
        UpdateLookBar();
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        // Force exactly one tab selected — clicking the already-selected tab
        // keeps it selected (no "both off" state).
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

    // --- snapshot trigger ----------------------------------------------------

    private static readonly SolidColorBrush s_errorBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x7A, 0xA4)); // soft coral
    private static readonly SolidColorBrush s_okBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0x54, 0xD2, 0xA6)); // efficiency teal

    private async void OnSnapshotClick(object sender, RoutedEventArgs e)
    {
        SnapshotBtn.IsEnabled = false;
        SnapshotBtn.Opacity = 0.4;
        ShowStatus("Working...", isError: false);

        string app = "Unknown";
        string title = string.Empty;

        try
        {
            var apiKey = ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ShowStatus("Set ANTHROPIC_API_KEY", isError: true);
                await MomentLog.AppendFailureAsync(app, title, "ANTHROPIC_API_KEY not set");
                return;
            }

            var foreground = ForegroundContext.Read();
            app = foreground.App;
            title = foreground.WindowTitle;

            byte[] jpeg = await ScreenCapture.CaptureAsJpegAsync();
            string summary = await MomentExtractor.ExtractAsync(jpeg, foreground);

            var moment = new Moment(
                UlidGenerator.Generate(),
                DateTimeOffset.UtcNow,
                foreground.App,
                foreground.WindowTitle,
                summary);
            await MomentLog.AppendSuccessAsync(moment);

            ShowStatus("Saved", isError: false);
        }
        catch (Exception ex)
        {
            string message = ex.Message.Length > 60
                ? ex.Message.Substring(0, 60) + "..."
                : ex.Message;
            ShowStatus($"Error: {message}", isError: true);
            await MomentLog.AppendFailureAsync(app, title, ex.Message);
        }
        finally
        {
            SnapshotBtn.IsEnabled = true;
            SnapshotBtn.Opacity = 1.0;
        }
    }

    /// <summary>
    /// Resolve the API key in this order:
    /// 1. Process env (`ANTHROPIC_API_KEY`) — what the SDK reads by default.
    /// 2. User env target (registry — what `setx` writes).
    /// 3. A `huddle.env` file next to `Huddle.exe`.
    /// 4. A `huddle.env` file at `%LOCALAPPDATA%\Huddle\`.
    /// Whichever wins is promoted into the process env so the SDK sees it on subsequent calls.
    /// </summary>
    private static string? ResolveApiKey()
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(key)) return key;

        try
        {
            key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key);
                return key;
            }
        }
        catch { /* registry can throw in restricted contexts */ }

        foreach (var path in EnvFileCandidates())
        {
            key = ReadKeyFromEnvFile(path, "ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", key);
                return key;
            }
        }
        return null;
    }

    private static IEnumerable<string> EnvFileCandidates()
    {
        var exeDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(exeDir))
        {
            yield return System.IO.Path.Combine(exeDir, "huddle.env");
        }
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApp))
        {
            yield return System.IO.Path.Combine(localApp, "Huddle", "huddle.env");
        }
    }

    private static string? ReadKeyFromEnvFile(string path, string name)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            foreach (var rawLine in System.IO.File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var k = line.Substring(0, eq).Trim();
                if (!string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) continue;
                var v = line.Substring(eq + 1).Trim();
                if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
                {
                    v = v.Substring(1, v.Length - 2);
                }
                return v;
            }
        }
        catch { /* unreadable env file — ignore */ }
        return null;
    }

    /// <summary>
    /// Shows the snapshot status next to the section header. Stays visible
    /// until the next click — easier to read than a brief auto-hiding flash.
    /// </summary>
    private void ShowStatus(string text, bool isError)
    {
        SnapshotStatusText.Text = text;
        SnapshotStatusText.Foreground = isError ? s_errorBrush : s_okBrush;
        SnapshotStatusText.Visibility = Visibility.Visible;
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

        // Update the look-bar clip width so the gradient still fills horizontally
        // after the window resizes.
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
