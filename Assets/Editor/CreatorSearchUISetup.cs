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
        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Tools/Yobi/Setup Creator Search UI")]
        private static void SetupCreatorSearchUI()
        {
            if (Object.FindObjectOfType<CreatorSearchPanelBehaviour>() != null)
            {
                Debug.LogWarning("[CreatorSearchUISetup] A CreatorSearchPanelBehaviour already exists in the scene. Aborting to avoid duplicates.");
                return;
            }

            var canvasGo = CreateCanvas();
            EnsureEventSystem();

            var panelGo = CreatePanel(canvasGo.transform);
            var panelBehaviour = panelGo.AddComponent<CreatorSearchPanelBehaviour>();

            var searchRow = CreateHorizontalRow(panelGo.transform, "SearchRow");
            var inputField = CreateInputField(searchRow.transform, "SearchInputField");
            var searchButton = CreateButton(searchRow.transform, "SearchButton", "Search");

            var statusText = CreateText(panelGo.transform, "StatusText", string.Empty);

            CreateSection(panelGo.transform, "ResultsSection", "Search Results", out var resultsContainer);
            var resultRowTemplate = CreateResultRowTemplate(resultsContainer);

            CreateSection(panelGo.transform, "WatchlistSection", "Temporary Watchlist", out var watchlistContainer);
            var watchlistRowTemplate = CreateWatchlistRowTemplate(watchlistContainer);

            WireReferences(panelBehaviour, inputField, searchButton, statusText, resultsContainer, resultRowTemplate, watchlistContainer, watchlistRowTemplate);

            EditorUtility.SetDirty(panelGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = panelGo;
            Debug.Log("[CreatorSearchUISetup] Creator Search UI created. Remember to save the scene.");
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
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            // Project uses the new Input System exclusively (activeInputHandler = 1),
            // so the legacy StandaloneInputModule would leave the UI unresponsive.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
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

        private static void CreateSection(Transform parent, string sectionName, string labelText, out RectTransform container)
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

            container = containerGo.GetComponent<RectTransform>();
        }

        private static GameObject CreateResultRowTemplate(RectTransform container)
        {
            var rowGo = new GameObject("ResultRowTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(container, false);

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            rowGo.GetComponent<LayoutElement>().preferredHeight = 24f;

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            nameGo.transform.SetParent(rowGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = UiFont;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var addButton = CreateButton(rowGo.transform, "AddButton", "Add");
            addButton.GetComponent<LayoutElement>().preferredWidth = 60f;

            rowGo.SetActive(false);
            return rowGo;
        }

        private static GameObject CreateWatchlistRowTemplate(RectTransform container)
        {
            var rowGo = new GameObject("WatchlistRowTemplate", typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(container, false);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 20f;

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(rowGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = UiFont;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            SetupStretch(nameGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

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
            Text statusText,
            RectTransform resultsContainer,
            GameObject resultRowTemplate,
            RectTransform watchlistContainer,
            GameObject watchlistRowTemplate)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("searchInputField").objectReferenceValue = inputField;
            so.FindProperty("searchButton").objectReferenceValue = searchButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("resultsContainer").objectReferenceValue = resultsContainer;
            so.FindProperty("resultRowTemplate").objectReferenceValue = resultRowTemplate;
            so.FindProperty("watchlistContainer").objectReferenceValue = watchlistContainer;
            so.FindProperty("watchlistRowTemplate").objectReferenceValue = watchlistRowTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
