using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Huddle.Capture;

/// <summary>
/// Workstation lock detection on plumbing we own. Registers the given window
/// for WTS session notifications and raises <see cref="Locked"/> /
/// <see cref="Unlocked"/> from WM_WTSSESSION_CHANGE — delivered on the
/// window's own thread, so handlers may touch UI state directly. Also exposes
/// <see cref="IsSessionLocked"/> so callers can verify the real lock state
/// when a message was missed (SystemEvents.SessionSwitch silently dropped
/// the lock event, which is why this class exists).
/// </summary>
internal sealed class SessionLockWatcher : IDisposable
{
    private const uint SubclassId = 1;

    private readonly IntPtr _hwnd;
    // Held in a field so the GC can't collect the delegate behind the
    // native subclass registration.
    private SUBCLASSPROC? _subclassProc;
    private bool _registered;

    public event EventHandler? Locked;
    public event EventHandler? Unlocked;

    public SessionLockWatcher(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = OnSubclassMessage;

        if (!SetWindowSubclass(hwnd, _subclassProc, SubclassId, UIntPtr.Zero))
        {
            Debug.WriteLine("[Huddle] SetWindowSubclass failed — lock events disabled");
            _subclassProc = null;
            return;
        }

        if (!WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION))
        {
            Debug.WriteLine($"[Huddle] WTSRegisterSessionNotification failed ({Marshal.GetLastWin32Error()}) — lock events disabled");
            RemoveWindowSubclass(hwnd, _subclassProc, SubclassId);
            _subclassProc = null;
            return;
        }

        _registered = true;
    }

    public void Dispose()
    {
        if (_registered)
        {
            WTSUnRegisterSessionNotification(_hwnd);
            _registered = false;
        }
        if (_subclassProc is not null)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
            _subclassProc = null;
        }
    }

    /// <summary>
    /// Queries the session's actual lock state. Fails open: a failed query or
    /// an unknown state reports unlocked, so a broken query can never stall
    /// the capture loop — the message-based events still cover locks then.
    /// </summary>
    public static bool IsSessionLocked()
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, WTS_CURRENT_SESSION, WTSSessionInfoEx, out IntPtr buffer, out uint bytes))
        {
            return false;
        }

        try
        {
            if (bytes < Marshal.SizeOf<WTSINFOEX>()) return false;
            var info = Marshal.PtrToStructure<WTSINFOEX>(buffer);
            return info.Level == 1 && info.SessionFlags == WTS_SESSIONSTATE_LOCK;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private IntPtr OnSubclassMessage(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == WM_WTSSESSION_CHANGE)
        {
            switch ((int)wParam)
            {
                case WTS_SESSION_LOCK:
                    Locked?.Invoke(this, EventArgs.Empty);
                    break;
                case WTS_SESSION_UNLOCK:
                    Unlocked?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    // --- Win32 ------------------------------------------------------------

    private const uint WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOCK = 0x7;
    private const int WTS_SESSION_UNLOCK = 0x8;
    private const uint NOTIFY_FOR_THIS_SESSION = 0;

    private const uint WTS_CURRENT_SESSION = 0xFFFFFFFF;
    private const int WTSSessionInfoEx = 25;
    // Note the inverted-looking values — these are as documented for Win8+.
    private const int WTS_SESSIONSTATE_LOCK = 0;

    // Minimal view of WTSINFOEXW: Level, then a union whose LARGE_INTEGER
    // members give it 8-byte alignment (so the union starts at offset 8);
    // inside WTSINFOEX_LEVEL1_W, SessionFlags follows SessionId (4) and
    // SessionState (4).
    [StructLayout(LayoutKind.Explicit)]
    private struct WTSINFOEX
    {
        [FieldOffset(0)] public uint Level;
        [FieldOffset(16)] public int SessionFlags;
    }

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr hServer, uint sessionId, int wtsInfoClass, out IntPtr buffer, out uint bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
