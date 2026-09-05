using UnityEngine;
using UnityEngine.UI;

namespace Yobi.Presentation
{
    // The circular icon buttons along the Room UI's edge (Search / AI Query / Switch Mode, plus
    // the Settings button SettingsModalUISetup adds to the same dock) - styled to blend into the
    // background per the reference layout (white fill, black outline, slight transparency)
    // rather than opaque debug-UI buttons.
    public sealed class RoomButtonDockBehaviour : MonoBehaviour
    {
        private const int CircleTextureDiameter = 128;

        [SerializeField]
        private Button searchToggleButton;

        [SerializeField]
        private Button aiQueryToggleButton;

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

            // Applied to every Button under this dock, not just the ones this script has fields
            // for, so a button another tool adds to the same dock (SettingsModalUISetup's
            // Settings button) automatically gets the same circular treatment without this
            // script needing to know about it. Each starts as a plain square Image (border) with
            // a square "Fill" child Image from the Editor tool - the actual circular shape is
            // generated in code here rather than as a checked-in art asset.
            foreach (var button in GetComponentsInChildren<Button>(includeInactive: true))
            {
                ApplyCircularSprites(button);
            }

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

            if (switchModeButton != null)
            {
                switchModeButton.onClick.AddListener(OnSwitchModeButtonClicked);
            }
        }

        // Paints a real circular shape onto a button's border Image and its "Fill" child Image
        // using a soft 1px edge so the circle doesn't look jagged.
        private static void ApplyCircularSprites(Button button)
        {
            if (button == null)
            {
                return;
            }

            var borderImage = button.GetComponent<Image>();
            if (borderImage != null)
            {
                borderImage.sprite = CreateCircleSprite(CircleTextureDiameter, Color.white);
                borderImage.type = Image.Type.Simple;
            }

            var fillTransform = button.transform.Find("Fill");
            var fillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (fillImage != null)
            {
                fillImage.sprite = CreateCircleSprite(CircleTextureDiameter, Color.white);
                fillImage.type = Image.Type.Simple;
            }
        }

        private static Sprite CreateCircleSprite(int diameter, Color tint)
        {
            var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            var center = (diameter - 1) / 2f;
            var radius = diameter / 2f;
            var pixels = new Color32[diameter * diameter];

            for (var y = 0; y < diameter; y++)
            {
                for (var x = 0; x < diameter; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Anti-aliased edge: full alpha well inside the radius, fading to 0 over the
                    // outermost pixel instead of a hard, jagged boundary.
                    var alpha = Mathf.Clamp01(radius - distance);
                    var pixelColor = tint;
                    pixelColor.a *= alpha;
                    pixels[(y * diameter) + x] = pixelColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f));
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

        private void OnSwitchModeButtonClicked()
        {
            _companionWindow?.ToggleMode();
        }
    }
}
