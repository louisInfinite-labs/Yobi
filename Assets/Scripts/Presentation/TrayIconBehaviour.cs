using AOT;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Infrastructure.FilePicker;
using Yobi.Infrastructure.Tray;
using Yobi.Infrastructure.Window;

namespace Yobi.Presentation
{
    // Adds a menu bar icon with Show/Hide, mode switching, choosing the Room wallpaper, and
    // Quit, so the desktop companion window can be reached even before it has an actual
    // clickable character (roadmap Phase 3).
    public sealed class TrayIconBehaviour : MonoBehaviour
    {
        // Rooted here so the GC can't collect them: native code only holds the raw function
        // pointers marshalled from these delegate instances, which wouldn't keep them alive on
        // the managed side by themselves.
        private static readonly MacTrayIconControl.TrayActionCallback CallbackDelegate = OnTrayAction;
        private static readonly MacFilePicker.FilePickedCallback WallpaperPickedDelegate = OnWallpaperPicked;

        private static bool _callbackRegistered;

        // The callbacks are static (required for MonoPInvokeCallback), so they need static
        // handles to the objects they act on rather than instance references.
        private static DesktopCompanionWindowBehaviour _companionWindow;
        private static RoomBackgroundBehaviour _roomBackground;

        private void Awake()
        {
            // OSXPlayer only, never OSXEditor - see DesktopCompanionWindowBehaviour for why
            // (there is no separate player window/menu bar item to control in Play Mode).
            if (UnityEngine.Application.platform != RuntimePlatform.OSXPlayer)
            {
                return;
            }

            _companionWindow = FindFirstObjectByType<DesktopCompanionWindowBehaviour>();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            if (!_callbackRegistered)
            {
                MacTrayIconControl.SetActionCallback(CallbackDelegate);
                _callbackRegistered = true;
            }

            MacTrayIconControl.Create();

            if (_companionWindow != null)
            {
                MacTrayIconControl.SetToggleModeMenuTitle(DescribeToggleTarget(_companionWindow.CurrentMode));
            }
        }

        private void OnApplicationQuit()
        {
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacTrayIconControl.Remove();
            }
        }

        // Unlike MacNotificationScheduler's click callback (which UNUserNotificationCenter can
        // deliver on a background queue), NSMenu actions and NSOpenPanel's completion handler
        // are both dispatched on the main run loop - the same thread Unity's Standalone Player
        // pumps its own Update loop on - so it's safe to call Unity APIs here directly instead
        // of queuing and draining on Update().
        [MonoPInvokeCallback(typeof(MacTrayIconControl.TrayActionCallback))]
        private static void OnTrayAction(string action)
        {
            switch (action)
            {
                case "toggle_visibility":
                    MacWindowControl.SetVisible(!MacWindowControl.IsVisible());
                    break;
                case "toggle_mode":
                    if (_companionWindow != null)
                    {
                        var newMode = _companionWindow.ToggleMode();
                        MacTrayIconControl.SetToggleModeMenuTitle(DescribeToggleTarget(newMode));
                    }
                    break;
                case "choose_wallpaper":
                    MacFilePicker.ShowImageOpenPanel(WallpaperPickedDelegate);
                    break;
                case "quit":
                    UnityEngine.Application.Quit();
                    break;
            }
        }

        [MonoPInvokeCallback(typeof(MacFilePicker.FilePickedCallback))]
        private static void OnWallpaperPicked(string path)
        {
            if (_roomBackground != null)
            {
                _roomBackground.SetWallpaper(path);
            }
        }

        // The menu item names the mode switching *to*, not the one currently active - e.g.
        // while in DesktopMate mode, it reads "Switch to Room Mode".
        private static string DescribeToggleTarget(CompanionMode currentMode)
        {
            return currentMode == CompanionMode.DesktopMate ? "Switch to Room Mode" : "Switch to Desktop Mate Mode";
        }
    }
}
