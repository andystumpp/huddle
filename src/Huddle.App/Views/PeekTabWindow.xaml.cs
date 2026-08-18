using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;

namespace Huddle.Views;

public sealed partial class PeekTabWindow : Window
{
    public const int VisibleWidthDip = 28;
    public const int HeightDip = 168;
    public const int CornerRadiusDip = 14;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private Storyboard? _pulseStoryboard;
    private int _currentCount;

    public AppWindow PeekAppWindow => _appWindow;
    public IntPtr Hwnd => _hwnd;

    public PeekTabWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        Title = "Huddle peek";
        _appWindow.Title = "Huddle peek";

        ExtendsContentIntoTitleBar = true;
        if (_appWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            p.IsResizable = false;
            p.IsMinimizable = false;
            p.IsMaximizable = false;
            p.IsAlwaysOnTop = true;
        }

        // The tab is an on-screen widget, not a window to switch to — keep it out
        // of the taskbar and Alt-Tab so the app shows a single button (the panel).
        // AppWindow.IsShownInSwitchers does not drop the taskbar button on this SDK,
        // so mark it a tool window (WS_EX_TOOLWINDOW), which the shell excludes.
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        if (Content is FrameworkElement fe)
        {
            fe.Loaded += (_, _) => BuildPulseStoryboard();
        }
    }

    public void UpdateCount(int count)
    {
        _currentCount = count;
        CountText.Text = count.ToString();
        if (count > 0) StartPulse();
        else StopPulse();
    }

    /// <summary>
    /// Clip the window to the chip's rounded shape (physical px). The region
    /// is wider than the visible chip so the right rounded corners fall past
    /// the screen edge — on screen you see a tab with rounded left corners.
    /// </summary>
    public void ApplyRoundedRegion(int visibleWidthPx, int heightPx, int cornerRadiusPx)
    {
        var hRgn = CreateRoundRectRgn(0, 0, visibleWidthPx + cornerRadiusPx, heightPx + 1, cornerRadiusPx * 2, cornerRadiusPx * 2);
        SetWindowRgn(_hwnd, hRgn, bRedraw: true);
        // After SetWindowRgn succeeds the OS owns hRgn — do not delete it.
    }

    private void BuildPulseStoryboard()
    {
        // Soft halo expand+fade behind the count, mirrors the panel's WatchDot pulse.
        var scaleX = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(scaleX, PulseScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1.0 });
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800)), Value = 2.2, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400)), Value = 2.2 });

        var scaleY = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(scaleY, PulseScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1.0 });
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800)), Value = 2.2, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400)), Value = 2.2 });

        var opacity = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(opacity, PulseHalo);
        Storyboard.SetTargetProperty(opacity, "Opacity");
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0.55 });
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800)), Value = 0.0, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        opacity.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400)), Value = 0.0 });

        _pulseStoryboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = TimeSpan.FromMilliseconds(2400),
        };
        _pulseStoryboard.Children.Add(scaleX);
        _pulseStoryboard.Children.Add(scaleY);
        _pulseStoryboard.Children.Add(opacity);

        if (_currentCount > 0) _pulseStoryboard.Begin();
    }

    private void StartPulse() => _pulseStoryboard?.Begin();

    private void StopPulse()
    {
        _pulseStoryboard?.Stop();
        PulseHalo.Opacity = 0;
        PulseScale.ScaleX = 1;
        PulseScale.ScaleY = 1;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
}
