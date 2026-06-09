using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Huddle.Capture;

internal sealed record ForegroundInfo(string App, string WindowTitle);

internal static class ForegroundContext
{
    public static ForegroundInfo Read()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return new ForegroundInfo("Unknown", string.Empty);

        string title = GetWindowTitle(hwnd);
        string app = GetProcessExeName(hwnd);
        return new ForegroundInfo(app, title);
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
    }

    private static string GetProcessExeName(IntPtr hwnd)
    {
        try
        {
            _ = GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "Unknown";
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return "Unknown";
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
