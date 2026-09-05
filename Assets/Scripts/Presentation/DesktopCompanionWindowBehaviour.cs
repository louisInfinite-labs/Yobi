using System.Collections;
using UnityEngine;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Storage;
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

        private IWindowPositionRepository _windowPositionRepository;

        private void Awake()
        {
            _windowPositionRepository = new LocalFileWindowPositionRepository();

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
                RestoreWindowPosition();
                StartCoroutine(ApplyWindowSettingsOverFirstFrames());
            }
        }

        private void OnApplicationQuit()
        {
            if (UnityEngine.Application.platform != RuntimePlatform.OSXPlayer)
            {
                return;
            }

            MacWindowControl.GetPosition(out var x, out var y);
            _windowPositionRepository.Save(new WindowPosition(x, y));
        }

        private void RestoreWindowPosition()
        {
            var saved = _windowPositionRepository.Load();
            if (saved != null)
            {
                // Clamped natively against the currently connected screens' visible frames, in
                // case the position was saved on a monitor that isn't connected right now.
                MacWindowControl.SetPositionClamped(saved.X, saved.Y);
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
