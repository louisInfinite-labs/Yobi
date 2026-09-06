using UnityEngine;
using UnityEngine.UI;

namespace Yobi.EditorTools
{
    // Shared circular icon-button builder used by both RoomUIPanelSetup (Search/AI/Mode) and
    // SettingsModalUISetup (the Settings button added to the same dock) - keeps the GameObject
    // structure identical so RoomButtonDockBehaviour's "paint a circle onto every child Button"
    // pass at runtime works the same regardless of which tool created a given button.
    internal static class RoomButtonDockIconHelper
    {
        public static Button CreateCircularButtonWithCaption(Transform parent, string name, string iconGlyph, string caption, Font uiFont, Font iconFont)
        {
            var containerGo = new GameObject(name + "Container", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            containerGo.transform.SetParent(parent, false);

            var containerLayout = containerGo.GetComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 2f;
            containerLayout.childAlignment = TextAnchor.UpperCenter;
            containerLayout.childControlWidth = false;
            containerLayout.childControlHeight = false;
            containerGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var button = CreateCircularButton(containerGo.transform, name, iconGlyph, uiFont, iconFont);

            var captionGo = new GameObject(name + "Caption", typeof(RectTransform), typeof(Text));
            captionGo.transform.SetParent(containerGo.transform, false);
            var captionText = captionGo.GetComponent<Text>();
            captionText.font = uiFont;
            captionText.text = caption;
            captionText.color = Color.white;
            captionText.fontSize = 10;
            captionText.alignment = TextAnchor.MiddleCenter;
            captionGo.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 14f);

            return button;
        }

        // White fill, black border ring, slight transparency - a plain square here;
        // RoomButtonDockBehaviour paints the actual circular shape onto both Images at runtime
        // (generated in code, not a checked-in art asset).
        public static Button CreateCircularButton(Transform parent, string name, string iconGlyph, Font uiFont, Font iconFont)
        {
            var borderGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            borderGo.transform.SetParent(parent, false);
            borderGo.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);
            borderGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(borderGo.transform, false);
            SetupStretch(fillGo.GetComponent<RectTransform>(), new Vector2(2f, 2f), new Vector2(-2f, -2f));
            fillGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);

            var textGo = new GameObject("Icon", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(fillGo.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = iconFont != null ? iconFont : uiFont;
            text.text = iconGlyph;
            text.color = Color.black;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            SetupStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return borderGo.GetComponent<Button>();
        }

        private static void SetupStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
