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

        public static void MakeTransparent()
        {
            Yobi_MakeWindowTransparent();
        }

        public static void SetAlwaysOnTop(bool alwaysOnTop)
        {
            Yobi_SetWindowAlwaysOnTop(alwaysOnTop);
        }
    }
}
