using System.Runtime.InteropServices;

namespace Yobi.Infrastructure.Window
{
    // Bridges to Assets/Plugins/macOS/YobiWindowControl.bundle (source:
    // Native/YobiWindowControl.m). macOS Standalone Player only - see
    // DesktopCompanionWindowBehaviour for why this must never run inside the Editor.
    public static class MacWindowControl
    {
        private const string PluginName = "YobiWindowControl";

        [DllImport(PluginName)]
        private static extern void Yobi_MakeWindowTransparent();

        [DllImport(PluginName)]
        private static extern void Yobi_ApplyRoomWindowStyle();

        // [MarshalAs(UnmanagedType.I1)] on every bool here: the CLR's default P/Invoke
        // marshalling sends a 4-byte Win32 BOOL, but the native side's C99 `bool` (YobiWindowControl.m)
        // is 1 byte - without this, the two sides disagree on the argument/return size.
        [DllImport(PluginName)]
        private static extern void Yobi_SetWindowAlwaysOnTop([MarshalAs(UnmanagedType.I1)] bool alwaysOnTop);

        [DllImport(PluginName)]
        private static extern void Yobi_SetWindowVisible([MarshalAs(UnmanagedType.I1)] bool visible);

        [DllImport(PluginName)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Yobi_IsWindowVisible();

        [DllImport(PluginName)]
        private static extern void Yobi_GetWindowPosition(out double x, out double y);

        [DllImport(PluginName)]
        private static extern void Yobi_SetWindowPositionClamped(double x, double y);

        public static void MakeTransparent()
        {
            Yobi_MakeWindowTransparent();
        }

        public static void ApplyRoomStyle()
        {
            Yobi_ApplyRoomWindowStyle();
        }

        public static void SetAlwaysOnTop(bool alwaysOnTop)
        {
            Yobi_SetWindowAlwaysOnTop(alwaysOnTop);
        }

        public static void SetVisible(bool visible)
        {
            Yobi_SetWindowVisible(visible);
        }

        public static bool IsVisible()
        {
            return Yobi_IsWindowVisible();
        }

        public static void GetPosition(out double x, out double y)
        {
            Yobi_GetWindowPosition(out x, out y);
        }

        public static void SetPositionClamped(double x, double y)
        {
            Yobi_SetWindowPositionClamped(x, y);
        }
    }
}
