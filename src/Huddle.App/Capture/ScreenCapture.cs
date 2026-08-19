using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Huddle.Capture;

/// <summary>
/// Captures a frame and encodes it as JPEG. Two scopes, chosen by the caller:
/// the full primary display (rich multi-window context), or the active window only
/// (via <c>PrintWindow</c> — only the focused window's own pixels, so nothing
/// overlapping it is captured and the capture scope matches the foreground denylist
/// check exactly).
/// </summary>
internal static class ScreenCapture
{
    /// <param name="activeWindowOnly">
    /// True captures just the foreground window; false captures the whole primary display.
    /// </param>
    /// <param name="maxLongEdge">Longest edge of the resized image, in pixels.</param>
    /// <param name="qualityPercent">JPEG quality, 1-100.</param>
    public static async Task<byte[]> CaptureAsJpegAsync(
        bool activeWindowOnly = false,
        int maxLongEdge = 1280,
        int qualityPercent = 80)
    {
        try
        {
            (byte[] bgra, int width, int height) = activeWindowOnly
                ? GrabForegroundWindowBgra()
                : GrabPrimaryDisplayBgra();
            return await EncodeToJpegAsync(bgra, width, height, maxLongEdge, qualityPercent)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ScreenCaptureException)
        {
            throw new ScreenCaptureException("Capture failed.", ex);
        }
    }

    private static (byte[] bgra, int width, int height) GrabPrimaryDisplayBgra()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        if (width <= 0 || height <= 0)
        {
            throw new ScreenCaptureException($"Invalid screen size: {width}x{height}.");
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw new ScreenCaptureException("GetDC failed.");
        try
        {
            return RenderToBgra(screenDc, width, height,
                (memDc) =>
                {
                    if (!BitBlt(memDc, 0, 0, width, height, screenDc, 0, 0, SRCCOPY))
                    {
                        throw new ScreenCaptureException("BitBlt failed.");
                    }
                });
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static (byte[] bgra, int width, int height) GrabForegroundWindowBgra()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) throw new ScreenCaptureException("No foreground window.");
        if (IsIconic(hwnd)) throw new ScreenCaptureException("Foreground window is minimized.");
        if (!GetWindowRect(hwnd, out RECT rect)) throw new ScreenCaptureException("GetWindowRect failed.");

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new ScreenCaptureException($"Invalid window size: {width}x{height}.");
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) throw new ScreenCaptureException("GetDC failed.");
        try
        {
            return RenderToBgra(screenDc, width, height,
                (memDc) =>
                {
                    // PW_RENDERFULLCONTENT renders the window's own surface — including
                    // hardware-accelerated content (Chrome, Electron, WinUI) — so only
                    // this window's pixels are captured, not anything overlapping it.
                    if (!PrintWindow(hwnd, memDc, PW_RENDERFULLCONTENT))
                    {
                        throw new ScreenCaptureException("PrintWindow failed.");
                    }
                });
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    /// <summary>
    /// Allocates a top-down 32-bit DIB of <paramref name="width"/>×<paramref name="height"/>,
    /// runs <paramref name="draw"/> to paint into it, then reads the pixels back as BGRA.
    /// </summary>
    private static (byte[] bgra, int width, int height) RenderToBgra(
        IntPtr screenDc, int width, int height, Action<IntPtr> draw)
    {
        IntPtr memDc = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            memDc = CreateCompatibleDC(screenDc);
            if (memDc == IntPtr.Zero) throw new ScreenCaptureException("CreateCompatibleDC failed.");

            hBitmap = CreateCompatibleBitmap(screenDc, width, height);
            if (hBitmap == IntPtr.Zero) throw new ScreenCaptureException("CreateCompatibleBitmap failed.");

            IntPtr oldBitmap = SelectObject(memDc, hBitmap);
            try
            {
                draw(memDc);
            }
            finally
            {
                SelectObject(memDc, oldBitmap);
            }

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,         // negative = top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                },
            };
            var bgra = new byte[width * height * 4];
            int scanlines = GetDIBits(
                screenDc, hBitmap, 0, (uint)height, bgra, ref bmi, DIB_RGB_COLORS);
            if (scanlines == 0)
            {
                throw new ScreenCaptureException("GetDIBits returned 0 scanlines.");
            }
            return (bgra, width, height);
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
        }
    }

    private static async Task<byte[]> EncodeToJpegAsync(
        byte[] bgra, int srcWidth, int srcHeight, int maxLongEdge, int qualityPercent)
    {
        int longEdge = Math.Max(srcWidth, srcHeight);
        double scale = longEdge > maxLongEdge ? (double)maxLongEdge / longEdge : 1.0;
        uint targetWidth = (uint)Math.Max(1, Math.Round(srcWidth * scale));
        uint targetHeight = (uint)Math.Max(1, Math.Round(srcHeight * scale));

        using var stream = new InMemoryRandomAccessStream();
        var propertySet = new BitmapPropertySet
        {
            ["ImageQuality"] = new BitmapTypedValue(
                qualityPercent / 100.0, Windows.Foundation.PropertyType.Single),
        };
        var encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.JpegEncoderId, stream, propertySet);

        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)srcWidth,
            (uint)srcHeight,
            96.0, 96.0,
            bgra);

        if (targetWidth != srcWidth || targetHeight != srcHeight)
        {
            encoder.BitmapTransform.ScaledWidth = targetWidth;
            encoder.BitmapTransform.ScaledHeight = targetHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        await encoder.FlushAsync();

        stream.Seek(0);
        var buffer = new byte[stream.Size];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(buffer);
        return buffer;
    }

    // --- P/Invoke ---

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const uint SRCCOPY = 0x00CC0020;
    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] bmiColors;
    }
}

internal sealed class ScreenCaptureException : Exception
{
    public ScreenCaptureException(string message) : base(message) { }
    public ScreenCaptureException(string message, Exception inner) : base(message, inner) { }
}
