using System.Collections;
using UnityEngine;
using Yobi.Application.UseCases;
using Yobi.Domain.Entities;
using Yobi.Domain.Interfaces;
using Yobi.Infrastructure.Storage;
using Yobi.Infrastructure.Window;

namespace Yobi.Presentation
{
    // Owns the desktop companion window's two modes (roadmap Phase 3, "Desktop Companion 功能"):
    // DesktopMate - a borderless, transparent, always-on-top overlay, just the character
    // floating over the desktop; and Room - a normal-sized, resizable, opaque window showing the
    // character in a customizable scene (background wallpaper via RoomBackgroundBehaviour;
    // surrounding UI panels are a separate, not-yet-implemented follow-up). Dragging the
    // character and click-through for non-character pixels are also separate follow-ups.
    //
    // Supports macOS (MacWindowControl, a compiled native plugin, true per-pixel transparency)
    // and Windows (WindowsWindowControl, plain P/Invoke against user32.dll/dwmapi.dll, color-key
    // transparency - see its own comments for the tradeoff, and note it hasn't been runtime
    // -verified on real Windows hardware yet).
    public sealed class DesktopCompanionWindowBehaviour : MonoBehaviour
    {
        // Unity (re)creates its macOS CAMetalLayer as the graphics surface comes up, which can
        // happen after Awake and undoes the native opaque=NO fix applied too early. Reapplying
        // for the first few frames catches that recreation without needing a lower-level
        // render-thread hook. Reused whenever (re-)entering DesktopMate mode, not just at
        // startup, since it's cheap and it's simpler to have one path handle both.
        private const int ReapplyFrameCount = 30;

        [SerializeField]
        private Camera targetCamera;

        private IWindowPositionRepository _windowPositionRepository;
        private SwitchCompanionModeUseCase _switchModeUseCase;
        private Coroutine _desktopMateStyleCoroutine;
        private RoomBackgroundBehaviour _roomBackground;

        // Lazy rather than assigned in Awake(): TrayIconBehaviour reads CurrentMode from its
        // own Awake() to label the tray menu, and Unity doesn't guarantee Awake() order across
        // different GameObjects - this must work correctly regardless of which one runs first.
        private SwitchCompanionModeUseCase SwitchModeUseCase =>
            _switchModeUseCase ??= new SwitchCompanionModeUseCase(new LocalFileCompanionModeRepository(), CompanionMode.DesktopMate);

        public CompanionMode CurrentMode => SwitchModeUseCase.CurrentMode;

        // OSXPlayer/WindowsPlayer only, never the Editor: in the Editor, Play mode renders
        // inside the Game view panel docked in the Editor's own window - there is no separate
        // "player window" to find. The native/P-Invoke window search would instead grab whatever
        // window the Editor itself is running in, making the entire Unity Editor
        // transparent/borderless rather than just a game window.
        private static bool IsSupportedDesktopPlatform =>
            UnityEngine.Application.platform == RuntimePlatform.OSXPlayer ||
            UnityEngine.Application.platform == RuntimePlatform.WindowsPlayer;

        private void Awake()
        {
            _windowPositionRepository = new LocalFileWindowPositionRepository();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (IsSupportedDesktopPlatform)
            {
                RestoreWindowPosition();
                ApplyMode(SwitchModeUseCase.CurrentMode);
            }
        }

        private void OnApplicationQuit()
        {
            if (!IsSupportedDesktopPlatform)
            {
                return;
            }

            double x, y;
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacWindowControl.GetPosition(out x, out y);
            }
            else
            {
                WindowsWindowControl.GetPosition(out x, out y);
            }

            _windowPositionRepository.Save(new WindowPosition(x, y));
        }

        // Called from TrayIconBehaviour's menu action. Returns the new mode so the caller can
        // update the menu label without needing its own copy of the current mode.
        public CompanionMode ToggleMode()
        {
            if (!IsSupportedDesktopPlatform)
            {
                return SwitchModeUseCase.CurrentMode;
            }

            var newMode = SwitchModeUseCase.Toggle();
            ApplyMode(newMode);
            return newMode;
        }

        private void ApplyMode(CompanionMode mode)
        {
            // Stop any in-flight reapply loop from a previous DesktopMate entry first - without
            // this, switching to Room while it's still running would let its remaining frames
            // call MakeTransparent()/SetAlwaysOnTop(true) after ApplyRoomStyle(), undoing it.
            if (_desktopMateStyleCoroutine != null)
            {
                StopCoroutine(_desktopMateStyleCoroutine);
                _desktopMateStyleCoroutine = null;
            }

            var isMac = UnityEngine.Application.platform == RuntimePlatform.OSXPlayer;

            if (mode == CompanionMode.DesktopMate)
            {
                if (targetCamera != null)
                {
                    targetCamera.clearFlags = CameraClearFlags.SolidColor;
                    if (isMac)
                    {
                        // Alpha 0 so anything the character doesn't cover renders as fully
                        // transparent instead of a solid color, once the native side also makes
                        // the window itself see-through (Unity's own render target is opaque by
                        // default regardless of this - see YobiWindowControl.m).
                        targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    }
                    else
                    {
                        // WindowsWindowControl masks out this exact solid color at the OS level
                        // (color-key layered window) rather than relying on per-pixel alpha - see
                        // its own comments for why.
                        targetCamera.backgroundColor = WindowsWindowControl.TransparentKeyColor;
                    }
                }
                _desktopMateStyleCoroutine = StartCoroutine(ApplyDesktopMateStyleOverFirstFrames());
                _roomBackground?.SetVisible(false);
            }
            else
            {
                if (targetCamera != null)
                {
                    // Falls back to plain black behind the wallpaper Image if the user hasn't
                    // chosen one yet, or RoomBackgroundBehaviour isn't in the scene.
                    targetCamera.clearFlags = CameraClearFlags.SolidColor;
                    targetCamera.backgroundColor = Color.black;
                }

                if (isMac)
                {
                    MacWindowControl.ApplyRoomStyle();
                    MacWindowControl.SetAlwaysOnTop(false);
                }
                else
                {
                    WindowsWindowControl.ApplyRoomStyle();
                    WindowsWindowControl.SetAlwaysOnTop(false);
                }

                _roomBackground?.SetVisible(true);
            }
        }

        private void RestoreWindowPosition()
        {
            var saved = _windowPositionRepository.Load();
            if (saved == null)
            {
                return;
            }

            // Clamped natively against the currently connected screens' visible frames, in case
            // the position was saved on a monitor that isn't connected right now.
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacWindowControl.SetPositionClamped(saved.X, saved.Y);
            }
            else
            {
                WindowsWindowControl.SetPositionClamped(saved.X, saved.Y);
            }
        }

        private IEnumerator ApplyDesktopMateStyleOverFirstFrames()
        {
            var isMac = UnityEngine.Application.platform == RuntimePlatform.OSXPlayer;

            for (int frame = 0; frame < ReapplyFrameCount; frame++)
            {
                if (isMac)
                {
                    MacWindowControl.MakeTransparent();
                    MacWindowControl.SetAlwaysOnTop(true);
                }
                else
                {
                    WindowsWindowControl.MakeTransparent();
                    WindowsWindowControl.SetAlwaysOnTop(true);
                }

                yield return null;
            }
        }
    }
}
