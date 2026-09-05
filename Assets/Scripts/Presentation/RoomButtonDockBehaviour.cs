using AOT;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Infrastructure.FilePicker;

namespace Yobi.Presentation
{
    // The circular icon buttons along the Room UI's edge (Search / AI Query / Wallpaper /
    // Switch Mode) - styled to blend into the background per the reference layout (white fill,
    // black outline, slight transparency) rather than opaque debug-UI buttons.
    public sealed class RoomButtonDockBehaviour : MonoBehaviour
    {
        // Rooted so the GC can't collect it - see TrayIconBehaviour for why.
        private static readonly MacFilePicker.FilePickedCallback WallpaperPickedDelegate = OnWallpaperPicked;

        // Static because the callback is static (required for MonoPInvokeCallback).
        private static RoomBackgroundBehaviour _roomBackground;

        [SerializeField]
        private Button searchToggleButton;

        [SerializeField]
        private Button aiQueryToggleButton;

        [SerializeField]
        private Button wallpaperButton;

        [SerializeField]
        private Button switchModeButton;

        [SerializeField]
        private GameObject creatorSearchPanel;

        [SerializeField]
        private GameObject aiQueryPanel;

        private DesktopCompanionWindowBehaviour _companionWindow;

        // Start(), not Awake(): Unity guarantees every active object's Awake() has already run
        // before any Start() runs. Deactivating creatorSearchPanel/aiQueryPanel here relies on
        // that - doing it from Awake() would race whichever of those two scripts' own Awake()
        // Unity happens to call first (unspecified without an explicit Script Execution Order),
        // risking disabling the GameObject before its own Awake() (which sets up its use cases)
        // ever ran.
        private void Start()
        {
            _companionWindow = FindFirstObjectByType<DesktopCompanionWindowBehaviour>();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            if (creatorSearchPanel == null)
            {
                var searchPanel = FindFirstObjectByType<CreatorSearchPanelBehaviour>(FindObjectsInactive.Include);
                creatorSearchPanel = searchPanel != null ? searchPanel.gameObject : null;
            }

            if (aiQueryPanel == null)
            {
                var aiPanel = FindFirstObjectByType<AiQueryPanelBehaviour>(FindObjectsInactive.Include);
                aiQueryPanel = aiPanel != null ? aiPanel.gameObject : null;
            }

            // These debug panels used to always be visible, overlapping the Room UI's clock and
            // button dock. Now that these buttons exist specifically to toggle them, default to
            // hidden until opened on demand instead of cluttering the screen unconditionally.
            if (creatorSearchPanel != null)
            {
                creatorSearchPanel.SetActive(false);
            }

            if (aiQueryPanel != null)
            {
                aiQueryPanel.SetActive(false);
            }

            if (searchToggleButton != null)
            {
                searchToggleButton.onClick.AddListener(() => TogglePanel(creatorSearchPanel));
            }

            if (aiQueryToggleButton != null)
            {
                aiQueryToggleButton.onClick.AddListener(() => TogglePanel(aiQueryPanel));
            }

            if (wallpaperButton != null)
            {
                wallpaperButton.onClick.AddListener(OnWallpaperButtonClicked);
            }

            if (switchModeButton != null)
            {
                switchModeButton.onClick.AddListener(OnSwitchModeButtonClicked);
            }
        }

        private static void TogglePanel(GameObject panel)
        {
            if (panel != null)
            {
                panel.SetActive(!panel.activeSelf);
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
