using UnityEngine;
using UnityEngine.UI;

namespace Yobi.Presentation
{
    // The circular icon buttons along the Room UI's edge (Switch Mode, plus the Settings button
    // SettingsModalUISetup adds to the same dock) - styled to blend into the background per the
    // reference layout (white fill, black outline, slight transparency) rather than opaque
    // debug-UI buttons. Search, AI Query, and Wallpaper used to live here too: Search/AI were
    // replaced by MainSearchBarBehaviour's always-visible unified search bar, and Wallpaper moved
    // into the Settings modal's Display tab.
    public sealed class RoomButtonDockBehaviour : MonoBehaviour
    {
        private const int CircleTextureDiameter = 128;

        [SerializeField]
        private Button switchModeButton;

        private DesktopCompanionWindowBehaviour _companionWindow;

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

            if (switchModeButton != null)
            {
                switchModeButton.onClick.AddListener(OnSwitchModeButtonClicked);
            }

            ForceLayoutRebuild();
        }

        // Runtime safety net: this dock is assembled by two separate Editor tools
        // (RoomUIPanelSetup for Mode, SettingsModalUISetup for Settings), each baking its own
        // button container's position into the scene independently. The baked positions can
        // look correctly separated in the saved scene file yet still render overlapping on the
        // first Play frame, because each container's real height depends on its own
        // ContentSizeFitter/font metrics settling - which isn't guaranteed to have happened
        // before the dock's VerticalLayoutGroup positions its children. Forcing an immediate
        // rebuild here, bottom-up (each container first, then the dock itself), guarantees the
        // buttons are correctly stacked the moment Play starts, regardless of what was baked at
        // edit-time or which Editor tool ran last.
        private void ForceLayoutRebuild()
        {
            var dockRect = transform as RectTransform;
            if (dockRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            foreach (RectTransform child in dockRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(dockRect);
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

        private void OnSwitchModeButtonClicked()
        {
            _companionWindow?.ToggleMode();
        }
    }
}
