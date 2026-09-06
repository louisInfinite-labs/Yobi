using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Yobi.Infrastructure.Window
{
    // Windows equivalent of MacWindowControl's static API - see DesktopCompanionWindowBehaviour
    // for how the two are selected by platform. Unlike macOS, this needs no compiled native
    // plugin bundle: user32.dll/dwmapi.dll are part of Windows itself, callable directly via
    // P/Invoke.
    //
    // NOT YET VERIFIED ON REAL WINDOWS HARDWARE - written and compile-checked on macOS only
    // (no Windows machine available in this environment). The Win32 APIs used here are
    // long-stable and widely documented, but this should be run once on an actual Windows build
    // before relying on it.
    //
    // Transparency is approximated via color-key layered windows (WS_EX_LAYERED +
    // LWA_COLORKEY on TransparentKeyColor) rather than true per-pixel alpha - Windows CAN do
    // per-pixel alpha via UpdateLayeredWindow, but that bypasses Unity's own swap-chain
    // presentation entirely and would need a custom present path, well beyond a v1. Any pixel
    // that happens to render as exactly TransparentKeyColor (pure magenta - chosen because it's
    // vanishingly unlikely to appear in character art, unlike black or white) becomes see-through
    // instead of just alpha=0 pixels; DesktopCompanionWindowBehaviour's camera background must be
    // set to that same color while this control is active for the two to agree on what's "empty".
    public static class WindowsWindowControl
    {
        // Opaque, not alpha=0: this has to actually be drawn as a solid, visible color for the OS
        // compositor's color-key masking to find and remove it - Unity's own per-pixel alpha
        // isn't part of the picture here the way it is for MacWindowControl's approach.
        public static readonly UnityEngine.Color TransparentKeyColor = new UnityEngine.Color(1f, 0f, 1f, 1f);

        private const int GwlExStyle = -20;
        private const int WsExLayered = 0x00080000;
        private const int WsExTransparent = 0x00000020;
        private const uint LwaColorKey = 0x1;
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoZOrder = 0x0004;
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private static readonly IntPtr HwndNoTopMost = new IntPtr(-2);

        // WS_OVERLAPPEDWINDOW: the normal bordered/resizable/minimizable/maximizable style Room
        // mode wants, restored by clearing WS_EX_LAYERED/WS_EX_TRANSPARENT rather than needing a
        // saved copy of the original style bits (Unity's default player style already matches this).
        private const int WsOverlappedWindow = 0x00CF0000;
        private const int GwlStyle = -16;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        private const uint MonitorDefaultToNearest = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public uint cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public uint dwFlags;
        }

        // Cached rather than looked up per-call: MainWindowHandle is only valid once the window
        // has actually been created and given a title, and repeated Process.GetCurrentProcess()
        // calls are needlessly expensive for something that never changes after startup.
        private static IntPtr _windowHandle = IntPtr.Zero;

        private static IntPtr GetHandle()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                _windowHandle = Process.GetCurrentProcess().MainWindowHandle;
            }

            return _windowHandle;
        }

        public static void MakeTransparent()
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var exStyle = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, exStyle | WsExLayered | WsExTransparent);

            var colorKey = ToColorRef(TransparentKeyColor);
            SetLayeredWindowAttributes(hwnd, colorKey, 0, LwaColorKey);
        }

        public static void ApplyRoomStyle()
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var exStyle = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, exStyle & ~WsExLayered & ~WsExTransparent);
            SetWindowLong(hwnd, GwlStyle, WsOverlappedWindow);
        }

        public static void SetAlwaysOnTop(bool alwaysOnTop)
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(hwnd, alwaysOnTop ? HwndTopMost : HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize);
        }

        public static void SetVisible(bool visible)
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(hwnd, visible ? SwShow : SwHide);
        }

        public static bool IsVisible()
        {
            var hwnd = GetHandle();
            return hwnd != IntPtr.Zero && IsWindowVisible(hwnd);
        }

        public static void GetPosition(out double x, out double y)
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
            {
                x = 0;
                y = 0;
                return;
            }

            x = rect.Left;
            y = rect.Top;
        }

        public static void SetPositionClamped(double x, double y)
        {
            var hwnd = GetHandle();
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var windowRect))
            {
                return;
            }

            var width = windowRect.Right - windowRect.Left;
            var height = windowRect.Bottom - windowRect.Top;

            var clampedX = x;
            var clampedY = y;

            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero)
            {
                var info = new MonitorInfo { cbSize = (uint)Marshal.SizeOf<MonitorInfo>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    var minX = info.rcWork.Left;
                    var maxX = info.rcWork.Right - width;
                    var minY = info.rcWork.Top;
                    var maxY = info.rcWork.Bottom - height;

                    clampedX = System.Math.Min(System.Math.Max(x, minX), System.Math.Max(minX, maxX));
                    clampedY = System.Math.Min(System.Math.Max(y, minY), System.Math.Max(minY, maxY));
                }
            }

            SetWindowPos(hwnd, IntPtr.Zero, (int)clampedX, (int)clampedY, 0, 0, SwpNoSize | SwpNoZOrder);
        }

        // COLORREF is 0x00BBGGRR, not the usual 0xRRGGBB.
        private static uint ToColorRef(UnityEngine.Color color)
        {
            var r = (byte)UnityEngine.Mathf.RoundToInt(color.r * 255f);
            var g = (byte)UnityEngine.Mathf.RoundToInt(color.g * 255f);
            var b = (byte)UnityEngine.Mathf.RoundToInt(color.b * 255f);
            return (uint)((b << 16) | (g << 8) | r);
        }
    }
}
