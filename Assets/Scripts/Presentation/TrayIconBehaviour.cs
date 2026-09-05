using AOT;
using UnityEngine;
using Yobi.Infrastructure.Tray;
using Yobi.Infrastructure.Window;

namespace Yobi.Presentation
{
    // Adds a menu bar icon with Show/Hide and Quit, so the desktop companion window can be
    // reached even before it has an actual clickable character (roadmap Phase 3).
    public sealed class TrayIconBehaviour : MonoBehaviour
    {
        // Rooted here so the GC can't collect it: native code only holds the raw function
        // pointer marshalled from this delegate instance, which wouldn't keep it alive on the
        // managed side by itself.
        private static readonly MacTrayIconControl.TrayActionCallback CallbackDelegate = OnTrayAction;

        private static bool _callbackRegistered;

        private void Awake()
        {
            // OSXPlayer only, never OSXEditor - see DesktopCompanionWindowBehaviour for why
            // (there is no separate player window/menu bar item to control in Play Mode).
            if (UnityEngine.Application.platform != RuntimePlatform.OSXPlayer)
            {
                return;
            }

            if (!_callbackRegistered)
            {
                MacTrayIconControl.SetActionCallback(CallbackDelegate);
                _callbackRegistered = true;
            }

            MacTrayIconControl.Create();
        }

        private void OnApplicationQuit()
        {
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacTrayIconControl.Remove();
            }
        }

        // Unlike MacNotificationScheduler's click callback (which UNUserNotificationCenter can
        // deliver on a background queue), NSMenu actions are dispatched on the main run loop -
        // the same thread Unity's Standalone Player pumps its own Update loop on - so it's safe
        // to call Unity APIs here directly instead of queuing and draining on Update().
        [MonoPInvokeCallback(typeof(MacTrayIconControl.TrayActionCallback))]
        private static void OnTrayAction(string action)
        {
            switch (action)
            {
                case "toggle_visibility":
                    MacWindowControl.SetVisible(!MacWindowControl.IsVisible());
                    break;
                case "quit":
                    UnityEngine.Application.Quit();
                    break;
            }
        }
    }
}
