using System.Runtime.InteropServices;

namespace Yobi.Infrastructure.FilePicker
{
    // Bridges to Assets/Plugins/macOS/YobiFilePicker.bundle (source: Native/YobiFilePicker.m).
    // macOS Standalone Player only.
    public static class MacFilePicker
    {
        private const string PluginName = "YobiFilePicker";

        // Null path means the user cancelled.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FilePickedCallback(string path);

        [DllImport(PluginName)]
        private static extern void Yobi_ShowImageOpenPanel(FilePickedCallback callback);

        public static void ShowImageOpenPanel(FilePickedCallback callback)
        {
            Yobi_ShowImageOpenPanel(callback);
        }
    }
}
