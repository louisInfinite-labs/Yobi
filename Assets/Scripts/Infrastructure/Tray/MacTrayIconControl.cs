using System.Runtime.InteropServices;

namespace Yobi.Infrastructure.Tray
{
    // Bridges to Assets/Plugins/macOS/YobiTrayIcon.bundle (source: Native/YobiTrayIcon.m).
    // macOS Standalone Player only - see TrayIconBehaviour for why this must never run inside
    // the Editor.
    public static class MacTrayIconControl
    {
        private const string PluginName = "YobiTrayIcon";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TrayActionCallback(string action);

        [DllImport(PluginName)]
        private static extern void Yobi_CreateTrayIcon();

        [DllImport(PluginName)]
        private static extern void Yobi_RemoveTrayIcon();

        [DllImport(PluginName)]
        private static extern void Yobi_SetTrayActionCallback(TrayActionCallback callback);

        public static void Create()
        {
            Yobi_CreateTrayIcon();
        }

        public static void Remove()
        {
            Yobi_RemoveTrayIcon();
        }

        public static void SetActionCallback(TrayActionCallback callback)
        {
            Yobi_SetTrayActionCallback(callback);
        }
    }
}
