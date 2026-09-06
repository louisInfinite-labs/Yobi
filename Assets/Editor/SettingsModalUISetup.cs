using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    // Builds the centered CONFIG-style Settings modal (Display / Sound / Other tabs) and a
    // "Settings" gear button in the Room button dock that opens it.
    internal static class SettingsModalUISetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Material Icons (Assets/Fonts/MaterialIcons-Regular.ttf, Apache License 2.0).
        private const string MaterialIconSettings = "\uE8B8";
        private static Font _iconFont;
        private static Font IconFont =>
            _iconFont != null ? _iconFont : (_iconFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MaterialIcons-Regular.ttf"));

        [MenuItem("Tools/Yobi/Setup Settings Modal")]
        private static void SetupSettingsModal()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvasGo = EnsureCanvas();
            var modalRoot = BuildModal(canvasGo.transform, out var behaviourRefs);
            var settingsButton = EnsureSettingsButtonInDock();

            var behaviour = modalRoot.GetComponent<SettingsModalBehaviour>();
            if (behaviour == null)
            {
                behaviour = modalRoot.AddComponent<SettingsModalBehaviour>();
            }

            var so = new SerializedObject(behaviour);
            so.FindProperty("modalRoot").objectReferenceValue = modalRoot;
            so.FindProperty("openButton").objectReferenceValue = settingsButton;
            so.FindProperty("closeButton").objectReferenceValue = behaviourRefs.CloseButton;
            so.FindProperty("displayTabButton").objectReferenceValue = behaviourRefs.DisplayTabButton;
            so.FindProperty("soundTabButton").objectReferenceValue = behaviourRefs.SoundTabButton;
            so.FindProperty("otherTabButton").objectReferenceValue = behaviourRefs.OtherTabButton;
            so.FindProperty("displayTabContent").objectReferenceValue = behaviourRefs.DisplayTabContent;
            so.FindProperty("soundTabContent").objectReferenceValue = behaviourRefs.SoundTabContent;
            so.FindProperty("otherTabContent").objectReferenceValue = behaviourRefs.OtherTabContent;
            so.FindProperty("languageDropdown").objectReferenceValue = behaviourRefs.LanguageDropdown;
            so.FindProperty("resolutionDropdown").objectReferenceValue = behaviourRefs.ResolutionDropdown;
            so.FindProperty("fullscreenToggle").objectReferenceValue = behaviourRefs.FullscreenToggle;
            so.FindProperty("wallpaperButton").objectReferenceValue = behaviourRefs.WallpaperButton;
            so.FindProperty("soundMuteToggle").objectReferenceValue = behaviourRefs.SoundMuteToggle;
            so.FindProperty("soundVolumeSlider").objectReferenceValue = behaviourRefs.SoundVolumeSlider;
            so.FindProperty("notificationsToggle").objectReferenceValue = behaviourRefs.NotificationsToggle;
            so.ApplyModifiedPropertiesWithoutUndo();

            // LayoutGroups rebuild lazily - without forcing it here, a row added before later
            // siblings (e.g. the Display tab's Language row, added first) can keep the stale
            // position it had when the container was smaller, rather than picking up where
            // later-added rows correctly ended up.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(modalRoot.GetComponent<RectTransform>());

            EditorUtility.SetDirty(modalRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SettingsModalUISetup] Settings modal created/updated. SaveScene returned {saved}.");
        }

        private static GameObject EnsureCanvas()
        {
            var existing = GameObject.Find("SettingsModalCanvas");
            if (existing != null && existing.GetComponent<Canvas>() != null)
            {
                return existing;
            }

            var canvasGo = new GameObject("SettingsModalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the Room UI panel canvas (sortingOrder 0) and the wallpaper (-100) so the
            // modal always sits on top.
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            return canvasGo;
        }

        private static Button EnsureSettingsButtonInDock()
        {
            var dockGo = GameObject.Find("RoomButtonDock");
            if (dockGo == null)
            {
                Debug.LogError("[SettingsModalUISetup] RoomButtonDock not found - run Tools > Yobi > Setup Room UI Panel first.");
                return null;
            }

            var existing = dockGo.transform.Find("SettingsButtonContainer");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            return RoomButtonDockIconHelper.CreateCircularButtonWithCaption(dockGo.transform, "SettingsButton", MaterialIconSettings, "Setting", UiFont, IconFont);
        }

        private readonly struct ModalRefs
        {
            public readonly Button CloseButton;
            public readonly Button DisplayTabButton;
            public readonly Button SoundTabButton;
            public readonly Button OtherTabButton;
            public readonly GameObject DisplayTabContent;
            public readonly GameObject SoundTabContent;
            public readonly GameObject OtherTabContent;
            public readonly Dropdown LanguageDropdown;
            public readonly Dropdown ResolutionDropdown;
            public readonly Toggle FullscreenToggle;
            public readonly Button WallpaperButton;
            public readonly Toggle SoundMuteToggle;
            public readonly Slider SoundVolumeSlider;
            public readonly Toggle NotificationsToggle;

            public ModalRefs(
                Button closeButton, Button displayTabButton, Button soundTabButton, Button otherTabButton,
                GameObject displayTabContent, GameObject soundTabContent, GameObject otherTabContent,
                Dropdown languageDropdown, Dropdown resolutionDropdown, Toggle fullscreenToggle, Button wallpaperButton,
                Toggle soundMuteToggle, Slider soundVolumeSlider, Toggle notificationsToggle)
            {
                CloseButton = closeButton;
                DisplayTabButton = displayTabButton;
                SoundTabButton = soundTabButton;
                OtherTabButton = otherTabButton;
                DisplayTabContent = displayTabContent;
                SoundTabContent = soundTabContent;
                OtherTabContent = otherTabContent;
                LanguageDropdown = languageDropdown;
                ResolutionDropdown = resolutionDropdown;
                FullscreenToggle = fullscreenToggle;
                WallpaperButton = wallpaperButton;
                SoundMuteToggle = soundMuteToggle;
                SoundVolumeSlider = soundVolumeSlider;
                NotificationsToggle = notificationsToggle;
            }
        }

        private static GameObject BuildModal(Transform canvasTransform, out ModalRefs refs)
        {
            var existingRoot = canvasTransform.Find("SettingsModalRoot");
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }

            var root = new GameObject("SettingsModalRoot", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasTransform, false);
            SetupStretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(420f, 360f);
            card.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.97f);

            var titleText = CreateText(card.transform, "TitleText", "CONFIG");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -14f);
            titleRect.sizeDelta = new Vector2(0f, 24f);

            var closeButton = CreateSmallButton(card.transform, "CloseButton", "X");
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(26f, 26f);

            var tabRow = new GameObject("TabRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabRow.transform.SetParent(card.transform, false);
            var tabRowRect = tabRow.GetComponent<RectTransform>();
            tabRowRect.anchorMin = new Vector2(0f, 1f);
            tabRowRect.anchorMax = new Vector2(1f, 1f);
            tabRowRect.pivot = new Vector2(0.5f, 1f);
            tabRowRect.anchoredPosition = new Vector2(0f, -46f);
            tabRowRect.sizeDelta = new Vector2(-20f, 30f);

            var tabRowLayout = tabRow.GetComponent<HorizontalLayoutGroup>();
            tabRowLayout.spacing = 6f;
            tabRowLayout.childControlWidth = true;
            tabRowLayout.childControlHeight = true;
            tabRowLayout.childForceExpandWidth = true;
            tabRowLayout.childForceExpandHeight = true;

            var displayTabButton = CreateTabButton(tabRow.transform, "DisplayTabButton", "顯示設定");
            var soundTabButton = CreateTabButton(tabRow.transform, "SoundTabButton", "聲音設定");
            var otherTabButton = CreateTabButton(tabRow.transform, "OtherTabButton", "その他");

            var contentArea = new GameObject("ContentArea", typeof(RectTransform));
            contentArea.transform.SetParent(card.transform, false);
            var contentRect = contentArea.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(16f, 16f);
            contentRect.offsetMax = new Vector2(-16f, -86f);

            var displayContent = BuildDisplayTab(contentArea.transform, out var languageDropdown, out var resolutionDropdown, out var fullscreenToggle, out var wallpaperButton);
            var soundContent = BuildSoundTab(contentArea.transform, out var soundMuteToggle, out var soundVolumeSlider);
            var otherContent = BuildOtherTab(contentArea.transform, out var notificationsToggle);

            refs = new ModalRefs(
                closeButton, displayTabButton, soundTabButton, otherTabButton,
                displayContent, soundContent, otherContent,
                languageDropdown, resolutionDropdown, fullscreenToggle, wallpaperButton,
                soundMuteToggle, soundVolumeSlider, notificationsToggle);

            return root;
        }

        private static GameObject BuildDisplayTab(Transform parent, out Dropdown languageDropdown, out Dropdown resolutionDropdown, out Toggle fullscreenToggle, out Button wallpaperButton)
        {
            var content = CreateTabContent(parent, "DisplayTabContent");

            CreateSettingRow(content.transform, "LanguageRow", "語言", out var languageGo);
            languageDropdown = languageGo.AddComponent<Dropdown>();
            ConfigureDropdownVisuals(languageDropdown);

            CreateSettingRow(content.transform, "ResolutionRow", "畫面比例", out var resolutionGo);
            resolutionDropdown = resolutionGo.AddComponent<Dropdown>();
            ConfigureDropdownVisuals(resolutionDropdown);

            CreateSettingRow(content.transform, "FullscreenRow", "全屏顯示", out var fullscreenGo);
            fullscreenToggle = fullscreenGo.AddComponent<Toggle>();
            ConfigureToggleVisuals(fullscreenToggle);

            CreateSettingRow(content.transform, "WallpaperRow", "背景圖片", out var wallpaperGo);
            wallpaperButton = wallpaperGo.AddComponent<Button>();
            var wallpaperImage = wallpaperGo.GetComponent<Image>();
            wallpaperImage.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            var wallpaperLabel = CreateText(wallpaperGo.transform, "Label", "選擇圖片...");
            wallpaperLabel.color = Color.black;
            wallpaperLabel.alignment = TextAnchor.MiddleCenter;
            SetupStretch(wallpaperLabel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return content;
        }

        private static GameObject BuildSoundTab(Transform parent, out Toggle soundMuteToggle, out Slider soundVolumeSlider)
        {
            var content = CreateTabContent(parent, "SoundTabContent");

            CreateSettingRow(content.transform, "MuteRow", "靜音", out var muteGo);
            soundMuteToggle = muteGo.AddComponent<Toggle>();
            ConfigureToggleVisuals(soundMuteToggle);

            CreateSettingRow(content.transform, "VolumeRow", "音量", out var volumeGo);
            soundVolumeSlider = volumeGo.AddComponent<Slider>();
            ConfigureSliderVisuals(soundVolumeSlider);

            var noteText = CreateText(content.transform, "Note", "（App 暫時未有聲音功能，此設定會保留待日後使用）");
            noteText.fontSize = 11;
            noteText.fontStyle = FontStyle.Italic;
            noteText.color = new Color(1f, 1f, 1f, 0.6f);
            var noteLayout = noteText.gameObject.AddComponent<LayoutElement>();
            noteLayout.preferredHeight = 34f;

            return content;
        }

        private static GameObject BuildOtherTab(Transform parent, out Toggle notificationsToggle)
        {
            var content = CreateTabContent(parent, "OtherTabContent");

            CreateSettingRow(content.transform, "NotificationsRow", "直播推送通知", out var notificationsGo);
            notificationsToggle = notificationsGo.AddComponent<Toggle>();
            ConfigureToggleVisuals(notificationsToggle);

            var noteText = CreateText(content.transform, "Note", "（更改於下次開啟 App 時生效）");
            noteText.fontSize = 11;
            noteText.fontStyle = FontStyle.Italic;
            noteText.color = new Color(1f, 1f, 1f, 0.6f);
            var noteLayout = noteText.gameObject.AddComponent<LayoutElement>();
            noteLayout.preferredHeight = 34f;

            return content;
        }

        private static GameObject CreateTabContent(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            // Anchored to the top edge only (not a full 0,0-1,1 stretch): the ContentSizeFitter
            // below sets sizeDelta.y to the content's preferred height, and on a rect stretched on
            // both Y anchors that growth is split evenly above and below its pivot - pushing the
            // first row up into the tab buttons and the last row down past the card's bottom edge,
            // which is exactly the overlap/overflow seen in testing. Anchoring to the top means the
            // same sizeDelta growth extends downward only, from a fixed top edge.
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 0f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            // Must be true: each row's LayoutElement.preferredHeight (30) is only a hint the
            // group uses when this is on. With it false, rows kept whatever height they already
            // had (Unity's default 100 for a freshly-created RectTransform), spacing every row
            // ~100px apart instead of ~40px and pushing the tab content well past the card.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            go.SetActive(false);
            return go;
        }

        // A labeled row: name on the left, the control (dropdown/toggle/slider/button) on the
        // right - the control GameObject itself is returned un-typed so callers can
        // AddComponent<T> the specific control they need onto it.
        private static void CreateSettingRow(Transform parent, string rowName, string label, out GameObject controlGo)
        {
            var row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            // Both true: without childControlWidth, a child's RectTransform keeps Unity's
            // default 100x100 size regardless of its LayoutElement.preferredWidth (that value
            // only affects how much horizontal space the row *reserves* for it, not the actual
            // rendered rect) - the label and control below rendered at the wrong size/position
            // and overlapped the tab row above until this was set to true.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.GetComponent<LayoutElement>().preferredHeight = 30f;

            var labelText = CreateText(row.transform, "Label", label);
            labelText.color = Color.white;
            var labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 110f;

            controlGo = new GameObject("Control", typeof(RectTransform), typeof(Image));
            controlGo.transform.SetParent(row.transform, false);
            controlGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
            var controlLayout = controlGo.AddComponent<LayoutElement>();
            controlLayout.preferredWidth = 180f;
            controlLayout.preferredHeight = 26f;
        }

        private static void ConfigureDropdownVisuals(Dropdown dropdown)
        {
            var captionGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            captionGo.transform.SetParent(dropdown.transform, false);
            var captionText = captionGo.GetComponent<Text>();
            captionText.font = UiFont;
            captionText.color = Color.black;
            captionText.alignment = TextAnchor.MiddleLeft;
            SetupStretch(captionGo.GetComponent<RectTransform>(), new Vector2(8f, 2f), new Vector2(-24f, -2f));
            dropdown.captionText = captionText;

            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(dropdown.transform, false);
            templateGo.SetActive(false);
            var templateRect = templateGo.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 120f);
            templateGo.GetComponent<Image>().color = Color.white;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            SetupStretch(viewportGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var scrollRect = templateGo.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
            scrollRect.horizontal = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);
            scrollRect.content = contentRect;

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 24f);

            var itemBackgroundGo = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBackgroundGo.transform.SetParent(itemGo.transform, false);
            SetupStretch(itemBackgroundGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            itemBackgroundGo.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var itemCheckmarkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            itemCheckmarkGo.transform.SetParent(itemGo.transform, false);
            var checkmarkRect = itemCheckmarkGo.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(16f, 16f);
            checkmarkRect.anchoredPosition = new Vector2(10f, 0f);
            itemCheckmarkGo.GetComponent<Image>().color = Color.black;

            var itemLabelGo = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelText = itemLabelGo.GetComponent<Text>();
            itemLabelText.font = UiFont;
            itemLabelText.color = Color.black;
            itemLabelText.alignment = TextAnchor.MiddleLeft;
            SetupStretch(itemLabelGo.GetComponent<RectTransform>(), new Vector2(24f, 1f), new Vector2(-4f, -1f));

            var itemToggle = itemGo.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackgroundGo.GetComponent<Image>();
            itemToggle.graphic = itemCheckmarkGo.GetComponent<Image>();
            itemToggle.isOn = true;

            dropdown.template = templateRect;
            dropdown.itemText = itemLabelText;
            dropdown.targetGraphic = dropdown.GetComponent<Image>();
        }

        private static void ConfigureToggleVisuals(Toggle toggle)
        {
            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundGo.transform.SetParent(toggle.transform, false);
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(26f, 26f);
            backgroundRect.anchoredPosition = new Vector2(13f, 0f);
            backgroundGo.GetComponent<Image>().color = Color.white;

            var checkmarkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmarkGo.transform.SetParent(backgroundGo.transform, false);
            SetupStretch(checkmarkGo.GetComponent<RectTransform>(), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            checkmarkGo.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 1f);

            toggle.targetGraphic = backgroundGo.GetComponent<Image>();
            toggle.graphic = checkmarkGo.GetComponent<Image>();
            toggle.isOn = false;
        }

        private static void ConfigureSliderVisuals(Slider slider)
        {
            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundGo.transform.SetParent(slider.transform, false);
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            backgroundGo.GetComponent<Image>().color = new Color(0.7f, 0.7f, 0.7f, 1f);

            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(slider.transform, false);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            SetupStretch(fillGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            fillGo.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.9f, 1f);

            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(slider.transform, false);
            SetupStretch(handleAreaGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            handleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(16f, 16f);
            handleGo.GetComponent<Image>().color = Color.white;

            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.handleRect = handleGo.GetComponent<RectTransform>();
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
        }

        private static Button CreateTabButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            var text = CreateText(go.transform, "Text", label);
            text.color = Color.black;
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleCenter;
            SetupStretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return go.GetComponent<Button>();
        }

        private static Button CreateSmallButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            var text = CreateText(go.transform, "Text", label);
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            SetupStretch(text.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return go.GetComponent<Button>();
        }

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
    }
}
