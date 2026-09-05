using AOT;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Infrastructure.FilePicker;

namespace Yobi.Presentation
{
    // The icon buttons along the Room UI's edge (Wallpaper / Switch Mode) - Search and AI Query
    // used to live here too, toggling separate debug panels via CanvasGroup, but were replaced by
    // MainSearchBarBehaviour's always-visible unified search bar.
    public sealed class RoomButtonDockBehaviour : MonoBehaviour
    {
        // Rooted so the GC can't collect it - see TrayIconBehaviour for why.
        private static readonly MacFilePicker.FilePickedCallback WallpaperPickedDelegate = OnWallpaperPicked;

        // Static because the callback is static (required for MonoPInvokeCallback).
        private static RoomBackgroundBehaviour _roomBackground;

        [SerializeField]
        private Button wallpaperButton;

        [SerializeField]
        private Button switchModeButton;

        private DesktopCompanionWindowBehaviour _companionWindow;

        private void Start()
        {
            _companionWindow = FindFirstObjectByType<DesktopCompanionWindowBehaviour>();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            if (wallpaperButton != null)
            {
                wallpaperButton.onClick.AddListener(OnWallpaperButtonClicked);
            }

            if (switchModeButton != null)
            {
                switchModeButton.onClick.AddListener(OnSwitchModeButtonClicked);
            }
        }

        private void OnWallpaperButtonClicked()
        {
            // MacFilePicker is a macOS-only native plugin - see TrayIconBehaviour's identical
            // gate for the same reason.
            if (UnityEngine.Application.platform == RuntimePlatform.OSXPlayer)
            {
                MacFilePicker.ShowImageOpenPanel(WallpaperPickedDelegate);
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

        private void OnSwitchModeButtonClicked()
        {
            _companionWindow?.ToggleMode();
        }
    }
}
