using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    // Builds the Room mode UI panel (clock, live/upcoming reminder list, and the circular
    // button dock) - mirrors CreatorSearchUISetup/AiQueryUISetup's find-or-create,
    // rebuild-generated-children-only approach.
    internal static class RoomUIPanelSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Tools/Yobi/Setup Room UI Panel")]
        private static void SetupRoomUIPanel()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvasGo = EnsureCanvas();

            SetupClock(canvasGo.transform);
            SetupReminderList(canvasGo.transform);
            SetupButtonDock(canvasGo.transform);

            EditorUtility.SetDirty(canvasGo);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RoomUIPanelSetup] Room UI panel created/updated. SaveScene returned {saved}.");
        }

        private static GameObject EnsureCanvas()
        {
            var existing = GameObject.Find("RoomUIPanelCanvas");
            if (existing != null && existing.GetComponent<Canvas>() != null)
            {
                return existing;
            }

            var canvasGo = new GameObject("RoomUIPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            return canvasGo;
        }

        private static void SetupClock(Transform canvasTransform)
        {
            var existing = Object.FindFirstObjectByType<RoomClockBehaviour>();
            GameObject clockGo;
            if (existing != null)
            {
                clockGo = existing.gameObject;
                DestroyGeneratedChild(clockGo.transform, "TimeText");
                DestroyGeneratedChild(clockGo.transform, "DateText");
            }
            else
            {
                clockGo = new GameObject("RoomClock", typeof(RectTransform));
                clockGo.transform.SetParent(canvasTransform, false);
            }

            var rect = clockGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(180f, 50f);

            var timeText = CreateText(clockGo.transform, "TimeText", "--:--");
            timeText.fontSize = 28;
            timeText.fontStyle = FontStyle.Bold;
            var timeRect = timeText.GetComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(0f, 1f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.pivot = new Vector2(0f, 1f);
            timeRect.anchoredPosition = Vector2.zero;
            timeRect.sizeDelta = new Vector2(0f, 32f);

            var dateText = CreateText(clockGo.transform, "DateText", "---");
            dateText.fontSize = 13;
            dateText.fontStyle = FontStyle.Normal;
            var dateRect = dateText.GetComponent<RectTransform>();
            dateRect.anchorMin = new Vector2(0f, 1f);
            dateRect.anchorMax = new Vector2(1f, 1f);
            dateRect.pivot = new Vector2(0f, 1f);
            dateRect.anchoredPosition = new Vector2(0f, -32f);
            dateRect.sizeDelta = new Vector2(0f, 18f);

            var behaviour = clockGo.GetComponent<RoomClockBehaviour>();
            if (behaviour == null)
            {
                behaviour = clockGo.AddComponent<RoomClockBehaviour>();
            }

            var so = new SerializedObject(behaviour);
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("dateText").objectReferenceValue = dateText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupReminderList(Transform canvasTransform)
        {
            var existing = Object.FindFirstObjectByType<RoomReminderListBehaviour>();
            GameObject panelGo;
            if (existing != null)
            {
                panelGo = existing.gameObject;
                DestroyGeneratedChild(panelGo.transform, "HeaderButton");
                DestroyGeneratedChild(panelGo.transform, "ContentPanel");

                // A scene built before this collapsible restructure parented RowContainer
                // directly under the root (there was no ContentPanel wrapper yet) - left alone,
                // that old copy stays a permanently-active sibling of the new ContentPanel,
                // rendering its template row's default-gray Dot/blank text at all times
                // regardless of the collapse toggle. The new RowContainer this method creates
                // lives inside ContentPanel instead, so this old direct child is always stale.
                DestroyGeneratedChild(panelGo.transform, "RowContainer");

                // A scene built before this collapsible restructure has an older RoomReminderList
                // root: RectTransform + Image only, no VerticalLayoutGroup/ContentSizeFitter, and
                // the background now belongs to HeaderButton/ContentPanel individually rather than
                // sitting on the root. Migrate it in place rather than requiring a from-scratch scene.
                var staleRootImage = panelGo.GetComponent<Image>();
                if (staleRootImage != null)
                {
                    Object.DestroyImmediate(staleRootImage);
                }

                if (panelGo.GetComponent<VerticalLayoutGroup>() == null)
                {
                    panelGo.AddComponent<VerticalLayoutGroup>();
                }

                if (panelGo.GetComponent<ContentSizeFitter>() == null)
                {
                    panelGo.AddComponent<ContentSizeFitter>();
                }
            }
            else
            {
                panelGo = new GameObject("RoomReminderList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                panelGo.transform.SetParent(canvasTransform, false);
            }

            // Collapsed by default, expanding downward from the header - width stays fixed at
            // 220 but height is driven by ContentSizeFitter (header alone when collapsed, header
            // + the 160-tall content panel once expanded), rather than the old fixed 220x160
            // always-visible panel.
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(220f, 0f);

            var rootLayout = panelGo.GetComponent<VerticalLayoutGroup>();
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerButton = CreateReminderHeaderButton(panelGo.transform, out var headerLabel);
            var contentPanel = CreateReminderContentPanel(panelGo.transform, out var containerRect, out var rowTemplate);

            var behaviour = panelGo.GetComponent<RoomReminderListBehaviour>();
            if (behaviour == null)
            {
                behaviour = panelGo.AddComponent<RoomReminderListBehaviour>();
            }

            var so = new SerializedObject(behaviour);
            so.FindProperty("headerButton").objectReferenceValue = headerButton;
            so.FindProperty("headerLabel").objectReferenceValue = headerLabel;
            so.FindProperty("contentPanel").objectReferenceValue = contentPanel;
            so.FindProperty("rowContainer").objectReferenceValue = containerRect;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateReminderHeaderButton(Transform parent, out Text label)
        {
            var go = new GameObject("HeaderButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            go.GetComponent<LayoutElement>().preferredHeight = 26f;

            label = CreateText(go.transform, "Text", "List Status ▼");
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            SetupStretch(label.GetComponent<RectTransform>(), new Vector2(8f, 0f), new Vector2(-8f, 0f));

            return go.GetComponent<Button>();
        }

        private static GameObject CreateReminderContentPanel(Transform parent, out RectTransform containerRect, out GameObject rowTemplate)
        {
            var panelGo = new GameObject("ContentPanel", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            panelGo.transform.SetParent(parent, false);
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            panelGo.GetComponent<LayoutElement>().preferredHeight = 160f;

            var containerGo = new GameObject("RowContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            containerGo.transform.SetParent(panelGo.transform, false);
            containerRect = containerGo.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(8f, 8f);
            containerRect.offsetMax = new Vector2(-8f, -8f);

            var containerLayout = containerGo.GetComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 4f;
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = true;
            containerLayout.childForceExpandWidth = true;
            containerLayout.childForceExpandHeight = false;
            containerGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            rowTemplate = CreateReminderRowTemplate(containerRect);

            // Collapsed by default - RoomReminderListBehaviour toggles this via the header
            // button click, matching the "list status ▼, click to open" layout asked for.
            panelGo.SetActive(false);

            return panelGo;
        }

        private static GameObject CreateReminderRowTemplate(RectTransform container)
        {
            var rowGo = new GameObject("RowTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(container, false);

            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowGo.GetComponent<LayoutElement>().preferredHeight = 20f;

            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            dotGo.transform.SetParent(rowGo.transform, false);
            dotGo.GetComponent<Image>().color = Color.gray;
            var dotLayout = dotGo.GetComponent<LayoutElement>();
            dotLayout.preferredWidth = 10f;
            dotLayout.preferredHeight = 10f;

            var nameText = CreateText(rowGo.transform, "NameText", string.Empty);
            nameText.fontSize = 12;
            nameText.fontStyle = FontStyle.Normal;
            var nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;

            var statusText = CreateText(rowGo.transform, "StatusText", string.Empty);
            statusText.fontSize = 11;
            statusText.alignment = TextAnchor.MiddleRight;
            var statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 50f;

            return rowGo;
        }

        private static void SetupButtonDock(Transform canvasTransform)
        {
            var existing = Object.FindFirstObjectByType<RoomButtonDockBehaviour>();
            GameObject dockGo;
            if (existing != null)
            {
                dockGo = existing.gameObject;

                // Destroy every existing child rather than a fixed set of names: an older
                // version of this tool created flat "SearchButton"/"AiQueryButton"/
                // "WallpaperButton"/etc. children with no "...Container" wrapper (Search/AI
                // moved to MainSearchBarBehaviour's unified search bar, Wallpaper moved into the
                // Settings modal's Display tab - neither is recreated by this method anymore), so
                // a name-based cleanup here would leave those orphaned alongside the newly
                // (re)created ones. "SettingsButtonContainer" is the one deliberate exception -
                // it's added to this same dock by SettingsModalUISetup.EnsureSettingsButtonInDock,
                // not by this method, which only (re)creates Mode below; destroying it here would
                // remove the Settings modal's only entry point until someone reruns that other tool.
                for (var i = dockGo.transform.childCount - 1; i >= 0; i--)
                {
                    var child = dockGo.transform.GetChild(i);
                    if (child.name == "SettingsButtonContainer")
                    {
                        continue;
                    }

                    Object.DestroyImmediate(child.gameObject);
                }
            }
            else
            {
                dockGo = new GameObject("RoomButtonDock", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                dockGo.transform.SetParent(canvasTransform, false);
            }

            var rect = dockGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(20f, 0f);
            rect.sizeDelta = new Vector2(44f, 0f);

            var layout = dockGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            dockGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var switchModeButton = RoomButtonDockIconHelper.CreateCircularButtonWithCaption(dockGo.transform, "SwitchModeButton", MaterialIconSwap, "Mode", UiFont, IconFont);

            var behaviour = dockGo.GetComponent<RoomButtonDockBehaviour>();
            if (behaviour == null)
            {
                behaviour = dockGo.AddComponent<RoomButtonDockBehaviour>();
            }

            var so = new SerializedObject(behaviour);
            so.FindProperty("switchModeButton").objectReferenceValue = switchModeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Material Icons (Assets/Fonts/MaterialIcons-Regular.ttf, Apache License 2.0) - glyphs
        // addressed by their standard codepoints rather than the newer "Material Symbols" set,
        // since this is the older, stable "MaterialIcons-Regular" font.
        private const string MaterialIconSwap = "\uE8D4";

        private static Font _iconFont;
        private static Font IconFont =>
            _iconFont != null ? _iconFont : (_iconFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MaterialIcons-Regular.ttf"));

        private static Text CreateText(Transform parent, string name, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            return text;
        }

        private static void SetupStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void DestroyGeneratedChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }
    }
}
