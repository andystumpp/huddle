using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Huddle.Capture;

/// <summary>
/// Raises <see cref="Changed"/> when the display configuration changes — a monitor is
/// added or removed, the resolution or DPI changes, or the work area moves. Subclasses the
/// given window (same mechanism as <see cref="SessionLockWatcher"/>) and fires off
/// WM_DISPLAYCHANGE and WM_SETTINGCHANGE/SPI_SETWORKAREA. Events arrive on the window's own
/// thread, so handlers may touch UI state directly. Callers debounce, since a single
/// topology change emits a burst of these messages.
/// </summary>
internal sealed class DisplayChangeWatcher : IDisposable
{
    // Distinct from SessionLockWatcher's id (1) so both subclasses can coexist on one hwnd.
    private const uint SubclassId = 2;

    private readonly IntPtr _hwnd;
    // Held in a field so the GC can't collect the delegate behind the native subclass.
    private SUBCLASSPROC? _subclassProc;

    public event EventHandler? Changed;

    public DisplayChangeWatcher(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = OnSubclassMessage;

        if (!SetWindowSubclass(hwnd, _subclassProc, SubclassId, UIntPtr.Zero))
        {
            Debug.WriteLine("[Huddle] SetWindowSubclass failed — display-change events disabled");
            _subclassProc = null;
        }
    }

    public void Dispose()
    {
        if (_subclassProc is not null)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
            _subclassProc = null;
        }
    }

    private IntPtr OnSubclassMessage(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == WM_DISPLAYCHANGE ||
            (uMsg == WM_SETTINGCHANGE && (int)wParam == SPI_SETWORKAREA))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // --- Win32 ------------------------------------------------------------

    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const int SPI_SETWORKAREA = 0x002F;

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
}
