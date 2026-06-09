using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace Huddle.Views;

public sealed partial class PeekPanelWindow : Window
{
    private const int PanelWidth = 384;
    private const int RightGap = 12;
    private const int BottomGap = 12;
    private const int HeightHeadroom = 84;
    private const int DesiredHeight = 420;

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;

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
    }

    public void ShowPanel()
    {
        PositionPanel();
        SetTopmost(true);
        _appWindow.Show(activateWindow: true);
        Activate();
    }

    private void ConfigureChrome()
    {
        ExtendsContentIntoTitleBar = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: true);
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
        else
        {
            // Fallback: opaque dark tint, still readable.
            if (Content is FrameworkElement fe)
            {
                fe.RequestedTheme = ElementTheme.Dark;
            }
            if (RootClip != null)
            {
                RootClip.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xB8, 0x24, 0x26, 0x2B));
            }
        }
    }

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
