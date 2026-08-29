using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    internal static class CreatorSearchUISetup
    {
        private const string ReminderSettingsAssetPath = "Assets/Settings/ReminderSettings.asset";

        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Tools/Yobi/Setup Creator Search UI")]
        private static void SetupCreatorSearchUI()
        {
            // Runs unconditionally (even if the panel already exists) so re-running this command
            // after an update is enough to pick up a newly shared ReminderSettings asset on scenes
            // set up before it existed - reminder configuration must stay one shared asset, not a
            // value copied into each behaviour.
            var reminderSettings = EnsureReminderSettingsAsset();
            WireReminderSettingsIntoExistingBehaviours(reminderSettings);

            var existingPanelBehaviour = Object.FindObjectOfType<CreatorSearchPanelBehaviour>();
            var isNewPanel = existingPanelBehaviour == null;

            GameObject panelGo;
            CreatorSearchPanelBehaviour panelBehaviour;
            GameObject canvasGo;

            if (existingPanelBehaviour != null)
            {
                // Preserve the existing panel GameObject/component - only its known generated
                // children get rebuilt below, so any panel-level tweaks (background color, size,
                // manual repositioning, etc.) survive a rerun untouched.
                panelBehaviour = existingPanelBehaviour;
                panelGo = existingPanelBehaviour.gameObject;

                var existingCanvas = panelGo.GetComponentInParent<Canvas>();
                if (existingCanvas != null)
                {
                    canvasGo = existingCanvas.gameObject;
                }
                else
                {
                    canvasGo = EnsureCanvasByName();
                    panelGo.transform.SetParent(canvasGo.transform, false);
                    Debug.LogWarning("[CreatorSearchUISetup] Existing CreatorSearchPanel had no Canvas ancestor; re-parented under CreatorSearchCanvas.");
                }
            }
            else
            {
                canvasGo = EnsureCanvasByName();
                panelGo = CreatePanel(canvasGo.transform);
                panelBehaviour = panelGo.AddComponent<CreatorSearchPanelBehaviour>();
            }

            EnsureEventSystem();

            // Only the tool-generated children (identified by their fixed names) are torn down
            // and rebuilt - anything else under the panel that the user added by hand is left alone.
            DestroyGeneratedChild(panelGo.transform, "SearchRow");
            DestroyGeneratedChild(panelGo.transform, "StatusText");
            DestroyGeneratedChild(panelGo.transform, "ResultsSection");
            DestroyGeneratedChild(panelGo.transform, "WatchlistSection");

            var searchRow = CreateHorizontalRow(panelGo.transform, "SearchRow");
            var inputField = CreateInputField(searchRow.transform, "SearchInputField");
            var searchButton = CreateButton(searchRow.transform, "SearchButton", "Search");

            var statusText = CreateText(panelGo.transform, "StatusText", string.Empty);

            CreateSection(panelGo.transform, "ResultsSection", "Search Results", out _, out var resultsContainer);
            var resultRowTemplate = CreateResultRowTemplate(resultsContainer);

            CreateSection(panelGo.transform, "WatchlistSection", "Temporary Watchlist", out var watchlistSection, out var watchlistContainer);
            var watchlistRowTemplate = CreateWatchlistRowTemplate(watchlistContainer);
            var refreshStatusButton = CreateButton(watchlistSection.transform, "RefreshStatusButton", "Refresh Status");

            WireReferences(panelBehaviour, inputField, searchButton, refreshStatusButton, statusText, resultsContainer, resultRowTemplate, watchlistContainer, watchlistRowTemplate);
            AssignReminderSettings(panelBehaviour, reminderSettings);

            EditorUtility.SetDirty(panelGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = panelGo;
            Debug.Log(isNewPanel
                ? "[CreatorSearchUISetup] Creator Search UI created (Canvas, panel, and all templates). Remember to save the scene."
                : "[CreatorSearchUISetup] Creator Search UI templates rebuilt in place on the existing panel (SearchRow, StatusText, ResultsSection, WatchlistSection); Canvas and panel preserved. Remember to save the scene.");
        }

        private static GameObject EnsureCanvasByName()
        {
            var existingGo = GameObject.Find("CreatorSearchCanvas");
            if (existingGo != null && existingGo.GetComponent<Canvas>() != null)
            {
                return existingGo;
            }

            return CreateCanvas();
        }

        private static void DestroyGeneratedChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static ReminderSettings EnsureReminderSettingsAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ReminderSettings>(ReminderSettingsAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<ReminderSettings>();
            AssetDatabase.CreateAsset(settings, ReminderSettingsAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CreatorSearchUISetup] Created shared reminder settings asset at {ReminderSettingsAssetPath}.");
            return settings;
        }

        private static void WireReminderSettingsIntoExistingBehaviours(ReminderSettings settings)
        {
            var youtubeChecker = Object.FindObjectOfType<YouTubeLivestreamCheckerBehaviour>();
            if (youtubeChecker != null)
            {
                AssignReminderSettings(youtubeChecker, settings);
            }

            var creatorPanel = Object.FindObjectOfType<CreatorSearchPanelBehaviour>();
            if (creatorPanel != null)
            {
                AssignReminderSettings(creatorPanel, settings);
            }
        }

        private static void AssignReminderSettings(Object target, ReminderSettings settings)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty("reminderSettings");
            if (prop == null || prop.objectReferenceValue != null)
            {
                return;
            }

            prop.objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static GameObject CreateCanvas()
        {
            var canvasGo = new GameObject("CreatorSearchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            return canvasGo;
        }

        private static void EnsureEventSystem()
        {
            // Project uses the new Input System exclusively (activeInputHandler = 1),
            // so a StandaloneInputModule - whether newly created or already present on an
            // existing EventSystem - would leave the generated uGUI controls unresponsive.
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                return;
            }

            var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static GameObject CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("CreatorSearchPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(parent, false);

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(420f, 0f);

            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            panelGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panelGo;
        }

        private static GameObject CreateHorizontalRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            go.GetComponent<LayoutElement>().preferredHeight = 30f;

            return go;
        }

        private static InputField CreateInputField(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 30f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = UiFont;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            SetupStretch(textGo.GetComponent<RectTransform>(), new Vector2(8f, 6f), new Vector2(-8f, -6f));

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = UiFont;
            placeholder.text = "Search creator name";
            placeholder.color = new Color(0f, 0f, 0f, 0.4f);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            SetupStretch(placeholderGo.GetComponent<RectTransform>(), new Vector2(8f, 6f), new Vector2(-8f, -6f));

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            return inputField;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 70f;
            layoutElement.preferredHeight = 26f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = UiFont;
            text.text = label;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            SetupStretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return go.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontStyle = FontStyle.Italic;

            go.GetComponent<LayoutElement>().preferredHeight = 20f;

            return text;
        }

        private static void CreateSection(Transform parent, string sectionName, string labelText, out GameObject section, out RectTransform container)
        {
            var sectionGo = new GameObject(sectionName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            sectionGo.transform.SetParent(parent, false);

            var sectionLayout = sectionGo.GetComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 4f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateText(sectionGo.transform, "Label", labelText);

            var containerGo = new GameObject("Container", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            containerGo.transform.SetParent(sectionGo.transform, false);

            var containerLayout = containerGo.GetComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 4f;
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = true;
            containerLayout.childForceExpandWidth = true;
            containerLayout.childForceExpandHeight = false;
            containerGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            section = sectionGo;
            container = containerGo.GetComponent<RectTransform>();
        }

        private static GameObject CreateResultRowTemplate(RectTransform container)
        {
            // Row now has two parts: a fixed-height HeaderRow (name + Status + Add buttons) and a
            // StatusText below it that only fills in once "Status" is checked for that specific
            // result - so the row must grow to fit, same pattern as the watchlist row template.
            var rowGo = new GameObject("ResultRowTemplate", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(container, false);

            var rowLayout = rowGo.GetComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 2f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var headerRow = CreateHorizontalRow(rowGo.transform, "HeaderRow");

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            nameGo.transform.SetParent(headerRow.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = UiFont;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var checkStatusButton = CreateButton(headerRow.transform, "CheckStatusButton", "Status");
            checkStatusButton.GetComponent<LayoutElement>().preferredWidth = 60f;

            var addButton = CreateButton(headerRow.transform, "AddButton", "Add");
            addButton.GetComponent<LayoutElement>().preferredWidth = 60f;

            var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            statusGo.transform.SetParent(rowGo.transform, false);
            var resultStatusText = statusGo.GetComponent<Text>();
            resultStatusText.font = UiFont;
            resultStatusText.color = new Color(1f, 1f, 1f, 0.85f);
            resultStatusText.alignment = TextAnchor.UpperLeft;
            resultStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            resultStatusText.fontSize = 12;

            rowGo.SetActive(false);
            return rowGo;
        }

        private static GameObject CreateWatchlistRowTemplate(RectTransform container)
        {
            // Watchlist rows now show multi-line status text (channel, URL, LIVE/UPCOMING/NONE,
            // current/upcoming livestream info), so the row must grow to fit its content instead
            // of a fixed height - a VerticalLayoutGroup + ContentSizeFitter on the row, driven by
            // the child Text's own preferred height, does that.
            var rowGo = new GameObject("WatchlistRowTemplate", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(container, false);

            var rowLayout = rowGo.GetComponent<VerticalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            nameGo.transform.SetParent(rowGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = UiFont;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;

            rowGo.SetActive(false);
            return rowGo;
        }

        private static void SetupStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void WireReferences(
            CreatorSearchPanelBehaviour panel,
            InputField inputField,
            Button searchButton,
            Button refreshStatusButton,
            Text statusText,
            RectTransform resultsContainer,
            GameObject resultRowTemplate,
            RectTransform watchlistContainer,
            GameObject watchlistRowTemplate)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("searchInputField").objectReferenceValue = inputField;
            so.FindProperty("searchButton").objectReferenceValue = searchButton;
            so.FindProperty("refreshStatusButton").objectReferenceValue = refreshStatusButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("resultsContainer").objectReferenceValue = resultsContainer;
            so.FindProperty("resultRowTemplate").objectReferenceValue = resultRowTemplate;
            so.FindProperty("watchlistContainer").objectReferenceValue = watchlistContainer;
            so.FindProperty("watchlistRowTemplate").objectReferenceValue = watchlistRowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
