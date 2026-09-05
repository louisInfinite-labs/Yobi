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
        private CanvasGroup _creatorSearchPanelGroup;
        private CanvasGroup _aiQueryPanelGroup;

        private void Start()
        {
            _companionWindow = FindFirstObjectByType<DesktopCompanionWindowBehaviour>();
            _roomBackground = FindFirstObjectByType<RoomBackgroundBehaviour>();

            if (creatorSearchPanel == null)
            {
                var searchPanel = FindFirstObjectByType<CreatorSearchPanelBehaviour>();
                creatorSearchPanel = searchPanel != null ? searchPanel.gameObject : null;
            }

            if (aiQueryPanel == null)
            {
                var aiPanel = FindFirstObjectByType<AiQueryPanelBehaviour>();
                aiQueryPanel = aiPanel != null ? aiPanel.gameObject : null;
            }

            // These debug panels used to always be visible, overlapping the Room UI's clock and
            // button dock. Hidden by default and toggled on demand via the Search/AI buttons
            // instead - but via a CanvasGroup (alpha/interactable/blocksRaycasts), never
            // GameObject.SetActive(false). Deactivating the whole GameObject would stop its
            // MonoBehaviour's own Start() from ever running if this runs first (Unity doesn't
            // guarantee Start() order across different objects, only that every Awake() runs
            // before any Start()) - which would permanently skip CreatorSearchPanelBehaviour's
            // polling loop, and would make it invisible to any *other* script's plain
            // FindFirstObjectByType call (default excludes inactive objects), such as
            // RoomReminderListBehaviour subscribing to its WatchlistStatusUpdated event.
            _creatorSearchPanelGroup = EnsureHiddenViaCanvasGroup(creatorSearchPanel);
            _aiQueryPanelGroup = EnsureHiddenViaCanvasGroup(aiQueryPanel);

            if (searchToggleButton != null)
            {
                searchToggleButton.onClick.AddListener(() => TogglePanel(_creatorSearchPanelGroup));
            }

            if (aiQueryToggleButton != null)
            {
                aiQueryToggleButton.onClick.AddListener(() => TogglePanel(_aiQueryPanelGroup));
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

        private static CanvasGroup EnsureHiddenViaCanvasGroup(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = panel.AddComponent<CanvasGroup>();
            }

            SetGroupVisible(group, false);
            return group;
        }

        private static void TogglePanel(CanvasGroup group)
        {
            if (group != null)
            {
                SetGroupVisible(group, group.alpha < 0.5f);
            }
        }

        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
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
