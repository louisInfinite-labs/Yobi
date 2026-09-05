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
        private static extern void Yobi_SetWindowAlwaysOnTop(bool alwaysOnTop);

        [DllImport(PluginName)]
        private static extern void Yobi_SetWindowVisible(bool visible);

        [DllImport(PluginName)]
        private static extern bool Yobi_IsWindowVisible();

        public static void MakeTransparent()
        {
            Yobi_MakeWindowTransparent();
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
    }
}
