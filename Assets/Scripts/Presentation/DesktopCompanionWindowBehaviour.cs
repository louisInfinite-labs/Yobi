using System.Collections;
using UnityEngine;
using Yobi.Infrastructure.Window;

namespace Yobi.Presentation
{
    // Turns Yobi's window into a borderless, transparent, (optionally) always-on-top overlay -
    // the foundation for a Desktop Mate-style character companion (roadmap Phase 3, "Desktop
    // Companion 功能"). Dragging the character and click-through for non-character pixels are
    // separate, not-yet-implemented follow-ups.
    public sealed class DesktopCompanionWindowBehaviour : MonoBehaviour
    {
        // Unity (re)creates its macOS CAMetalLayer as the graphics surface comes up, which can
        // happen after Awake and undoes the native opaque=NO fix applied too early. Reapplying
        // for the first few frames catches that recreation without needing a lower-level
        // render-thread hook.
        private const int ReapplyFrameCount = 30;

        [SerializeField]
        private bool alwaysOnTop = true;

        [SerializeField]
        private Camera targetCamera;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                // Alpha 0 so anything the character doesn't cover renders as fully
                // transparent instead of a solid color, once the native side also makes the
                // window itself see-through (Unity's own render target is opaque by default
                // regardless of this - see YobiWindowControl.m).
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            // OSXPlayer only, never OSXEditor: in the Editor, Play mode renders inside the
            // Game view panel docked in the Editor's own window - there is no separate
            // "player window" to find. YobiWindowControl.m's window search would instead grab
            // whatever NSWindow the Editor itself is running in, making the entire Unity
            // Editor transparent/borderless rather than a game window.
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                StartCoroutine(ApplyWindowSettingsOverFirstFrames());
            }
        }

        private IEnumerator ApplyWindowSettingsOverFirstFrames()
        {
            for (int frame = 0; frame < ReapplyFrameCount; frame++)
            {
                MacWindowControl.MakeTransparent();
                MacWindowControl.SetAlwaysOnTop(alwaysOnTop);
                yield return null;
            }
        }
    }
}
