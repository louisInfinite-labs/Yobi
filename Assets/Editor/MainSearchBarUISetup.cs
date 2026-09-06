using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    // Builds the single Google-homepage-style search bar that replaced the separate Search and
    // AI Query dock buttons/panels - a pill input field top-center on screen, with a collapsed
    // results/answer area below it that MainSearchBarBehaviour shows after a submission.
    internal static class MainSearchBarUISetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Material Icons (Assets/Fonts/MaterialIcons-Regular.ttf, Apache License 2.0) "search"
        // glyph, addressed by its standard codepoint - see RoomUIPanelSetup's identical convention.
        private const string MaterialIconSearch = "\uE8B6";

        // AI Mode toggle uses a plain "AI" text label rather than an icon glyph: this project's
        // MaterialIcons-Regular.ttf is the older, stable ~932-icon set (see RoomUIPanelSetup's
        // comment on the same font) which predates icons like auto_awesome/smart_toy, so there is
        // no glyph in it to reliably address by codepoint.
        private const string AiModeLabel = "AI";

        private static Font _iconFont;
        private static Font IconFont =>
            _iconFont != null ? _iconFont : (_iconFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/MaterialIcons-Regular.ttf"));

        [MenuItem("Tools/Yobi/Setup Main Search Bar")]
        internal static void SetupMainSearchBar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // This bar replaced AiQueryCanvas/AiQueryPanel (whose scripts are deleted), but a
            // scene built before that change still has the leftover GameObjects sitting around -
            // deleting a script's .cs file does not remove GameObjects that reference it from an
            // already-saved scene, it just leaves the component with a missing script. Clean it
            // up here so anyone rerunning this tool on an older scene doesn't have to do it by hand.
            var staleAiCanvas = GameObject.Find("AiQueryCanvas");
            if (staleAiCanvas != null)
            {
                Object.DestroyImmediate(staleAiCanvas);
            }

            var existingBehaviour = Object.FindFirstObjectByType<MainSearchBarBehaviour>();
            var isNew = existingBehaviour == null;

            GameObject barGo;
            MainSearchBarBehaviour behaviour;

            if (existingBehaviour != null)
            {
                behaviour = existingBehaviour;
                barGo = existingBehaviour.gameObject;
                ApplyBarLayout(barGo.GetComponent<RectTransform>());
            }
            else
            {
                var canvasGo = EnsureCanvas();
                barGo = CreateBar(canvasGo.transform);
                behaviour = barGo.AddComponent<MainSearchBarBehaviour>();
            }

            EnsureEventSystem();

            DestroyGeneratedChild(barGo.transform, "SearchInputField");
            DestroyGeneratedChild(barGo.transform, "ResultsContainer");
            DestroyGeneratedChild(barGo.transform, "AnswerText");

            var inputField = CreateInputField(barGo.transform, "SearchInputField", out var searchIconButton, out var aiModeToggleButton, out var aiModeToggleBackground);
            var backgroundImage = inputField.GetComponent<Image>();

            var resultsContainer = CreateResultsContainer(barGo.transform, out var resultRowTemplate);
            var answerText = CreateAnswerText(barGo.transform, "AnswerText");

            WireReferences(behaviour, inputField, searchIconButton, aiModeToggleButton, aiModeToggleBackground, backgroundImage, resultsContainer, resultRowTemplate, answerText);

            EditorUtility.SetDirty(barGo);
            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = barGo;
            Debug.Log(isNew
                ? $"[MainSearchBarUISetup] Main search bar created. SaveScene returned {saved}."
                : $"[MainSearchBarUISetup] Main search bar rebuilt in place. SaveScene returned {saved}.");
        }

        private static GameObject EnsureCanvas()
        {
            var existingGo = GameObject.Find("MainSearchCanvas");
            if (existingGo != null && existingGo.GetComponent<Canvas>() != null)
            {
                return existingGo;
            }

            var canvasGo = new GameObject("MainSearchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            return canvasGo;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
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

        private static void DestroyGeneratedChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static void ApplyBarLayout(RectTransform rect)
        {
            // Top-center, ~15% of screen height down from the top - a Google-homepage-style
            // placement rather than a corner popup or a dock-triggered panel.
            //
            // Width capped at 300 (not a rounder 400+): RoomUIPanelSetup's RoomReminderList sits
            // top-right anchored at (1,1), offset (-20,-20), sized 220x160 - in this 800-wide
            // reference canvas that occupies local x:560-780. Centered at x=400, this bar's right
            // edge must stay under x=560 to clear it; a wider bar visibly overlapped that panel's
            // translucent black background, showing as an unexplained dark band along part of the
            // bar until traced back to this collision.
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -90f);
            rect.sizeDelta = new Vector2(300f, 0f);
        }

        private static GameObject CreateBar(Transform parent)
        {
            var barGo = new GameObject("MainSearchBar", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            barGo.transform.SetParent(parent, false);

            ApplyBarLayout(barGo.GetComponent<RectTransform>());

            var layout = barGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            barGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return barGo;
        }

        // A plain white square placeholder - MainSearchBarBehaviour paints the actual rounded-pill
        // shape onto this Image at runtime (generated in code, not a checked-in art asset), same
        // convention as the dock's circular buttons.
        private static InputField CreateInputField(Transform parent, string name, out Button searchIconButton, out Button aiModeToggleButton, out Image aiModeToggleBackground)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 44f;

            // Right edge reserved for the AI Mode toggle + search icon buttons below (92px, not
            // the full 20px margin every other side uses) so typed/placeholder text never runs
            // under either of them.
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = UiFont;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            // 12, not Unity's Text default (14) - at 14 the placeholder string below didn't fit
            // the ~188px available (300 bar width minus the 20+92 reserved margins) on one line
            // and wrapped to two, which a single-line pill input shouldn't do.
            text.fontSize = 12;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            SetupStretch(textGo.GetComponent<RectTransform>(), new Vector2(20f, 6f), new Vector2(-92f, -6f));

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = UiFont;
            placeholder.text = "搜尋創作者,或者問AI一個問題...";
            placeholder.color = new Color(0f, 0f, 0f, 0.4f);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.fontSize = 12;
            placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholder.verticalOverflow = VerticalWrapMode.Overflow;
            SetupStretch(placeholderGo.GetComponent<RectTransform>(), new Vector2(20f, 6f), new Vector2(-92f, -6f));

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            searchIconButton = CreateSearchIconButton(go.transform);
            aiModeToggleButton = CreateAiModeToggleButton(go.transform, out aiModeToggleBackground);

            return inputField;
        }

        // A plain icon button (not one of the dock's circular buttons - this lives inside the
        // pill, not the dock) sitting inside the input field's own right edge, matching a
        // Google-style search box's icon placement.
        private static Button CreateSearchIconButton(Transform parent)
        {
            var go = new GameObject("SearchIconButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-8f, 0f);
            rect.sizeDelta = new Vector2(36f, 36f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Text));
            iconGo.transform.SetParent(go.transform, false);
            var iconText = iconGo.GetComponent<Text>();
            iconText.font = IconFont != null ? IconFont : UiFont;
            iconText.text = MaterialIconSearch;
            iconText.color = new Color(0f, 0f, 0f, 0.6f);
            iconText.fontSize = 20;
            iconText.alignment = TextAnchor.MiddleCenter;
            SetupStretch(iconGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return button;
        }

        // Sits immediately left of the search icon button, inside the same pill. Off by default
        // (near-invisible background, matching the search icon's own resting look) - MainSearchBar
        // Behaviour fills the background in with an accent color once toggled on, so the "AI"
        // label alone doesn't have to carry that state.
        private static Button CreateAiModeToggleButton(Transform parent, out Image background)
        {
            var go = new GameObject("AiModeToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            // -8 (right margin) - 36 (search icon width) - 4 (gap) = -48.
            rect.anchoredPosition = new Vector2(-48f, 0f);
            rect.sizeDelta = new Vector2(36f, 36f);

            background = go.GetComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = background;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = UiFont;
            label.text = AiModeLabel;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0f, 0f, 0f, 0.6f);
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            SetupStretch(labelGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            return button;
        }

        private static RectTransform CreateResultsContainer(Transform parent, out GameObject resultRowTemplate)
        {
            var go = new GameObject("ResultsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            go.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var background = go.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            resultRowTemplate = CreateResultRowTemplate(go.transform);

            return go.GetComponent<RectTransform>();
        }

        private static GameObject CreateResultRowTemplate(Transform parent)
        {
            // Image+Button on the row root itself (not just AddButton) so a history row - which
            // hides AddButton entirely - can still be clicked anywhere to re-search; a Button
            // needs a raycastable Graphic to receive clicks at all, hence the near-invisible Image.
            var row = new GameObject("ResultRowTemplate", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            var rowImage = row.GetComponent<Image>();
            rowImage.color = new Color(1f, 1f, 1f, 0.05f);
            row.GetComponent<Button>().targetGraphic = rowImage;

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            // 16px left/right - NameText used to start flush against the row's own background
            // rect (no padding at all), reading as uncomfortably tight against that edge.
            layout.padding = new RectOffset(16, 16, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.GetComponent<LayoutElement>().preferredHeight = 30f;

            var nameText = CreateText(row.transform, "NameText", string.Empty);
            var nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontSize = 12;
            // Wrap instead of overflow, now that the row itself grows to fit (ContentSizeFitter
            // below) - a long channel name used to just overflow past NameText's own rect and
            // visually collide with StatusText next to it instead of wrapping to a second line.
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Overflow;

            // Fixed width, not flexible - "1小時59分後" needs to fit without pushing NameText
            // around from row to row; richText stays on Unity's Text default so
            // MainSearchBarBehaviour's "<color=...>●</color> Live" markup renders as intended.
            var statusText = CreateText(row.transform, "StatusText", string.Empty);
            var statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 80f;
            statusText.color = new Color(1f, 1f, 1f, 0.85f);
            statusText.alignment = TextAnchor.MiddleRight;
            statusText.fontSize = 11;

            // A plain "+" icon button instead of a "加落追蹤" text label - small and square so it
            // reads as an icon action rather than a second text label competing with NameText for
            // the eye. MainSearchBarBehaviour swaps its label to "✓" on click instead of relabeling
            // full words.
            //
            // Two-layer structure, not a single 28x28 Image+Button: the outer cell is what
            // childControlHeight/ContentSizeFitter above stretch to match a taller (wrapped
            // NameText) row, and fighting that stretch on the same object it's measured from
            // doesn't hold up across rows - a plain fixed preferredHeight here still rendered
            // visibly different square sizes row to row once the row itself grew taller than 30.
            // Anchoring a separate fixed 28x28 "Visual" child at the cell's own center instead
            // makes the square's actual size independent of however tall the outer cell ends up.
            var addButtonGo = new GameObject("AddButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            addButtonGo.transform.SetParent(row.transform, false);
            var addButtonHitArea = addButtonGo.GetComponent<Image>();
            addButtonHitArea.color = new Color(1f, 1f, 1f, 0f);
            addButtonGo.GetComponent<Button>().targetGraphic = addButtonHitArea;
            var addButtonLayout = addButtonGo.GetComponent<LayoutElement>();
            addButtonLayout.preferredWidth = 28f;
            addButtonLayout.minWidth = 28f;

            var addButtonVisual = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            addButtonVisual.transform.SetParent(addButtonGo.transform, false);
            addButtonVisual.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);
            var visualRect = addButtonVisual.GetComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.sizeDelta = new Vector2(28f, 28f);

            var addButtonText = CreateText(addButtonVisual.transform, "Text", "+");
            addButtonText.color = Color.black;
            addButtonText.fontSize = 16;
            addButtonText.fontStyle = FontStyle.Bold;
            addButtonText.alignment = TextAnchor.MiddleCenter;
            SetupStretch(addButtonText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            // Row height now follows NameText's wrapped content instead of a fixed 30 - childControl
            // Height (already true above) stretches StatusText/AddButton to match whatever that
            // ends up being, and minHeight keeps a single-line row from getting any shorter than
            // before.
            row.GetComponent<LayoutElement>().preferredHeight = -1f;
            row.GetComponent<LayoutElement>().minHeight = 30f;
            var rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            row.SetActive(false);
            return row;
        }

        // Image and Text as two separate GameObjects (not combined on one) - in this Unity
        // version, adding both Graphic-derived components via one GameObject(...) constructor
        // call silently drops the second one instead of erroring, leaving GetComponent<Text>()
        // null.
        private static Text CreateAnswerText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            SetupStretch(textGo.GetComponent<RectTransform>(), new Vector2(10f, 6f), new Vector2(-10f, -6f));

            var text = textGo.GetComponent<Text>();
            text.font = UiFont;
            text.text = string.Empty;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = 13;

            go.GetComponent<LayoutElement>().preferredHeight = 120f;

            return text;
        }

        private static Text CreateText(Transform parent, string name, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = UiFont;
            text.text = content;

            return text;
        }

        private static void SetupStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void WireReferences(
            MainSearchBarBehaviour behaviour,
            InputField inputField,
            Button searchIconButton,
            Button aiModeToggleButton,
            Image aiModeToggleBackground,
            Image backgroundImage,
            RectTransform resultsContainer,
            GameObject resultRowTemplate,
            Text answerText)
        {
            var so = new SerializedObject(behaviour);
            so.FindProperty("searchInputField").objectReferenceValue = inputField;
            so.FindProperty("searchIconButton").objectReferenceValue = searchIconButton;
            so.FindProperty("aiModeToggleButton").objectReferenceValue = aiModeToggleButton;
            so.FindProperty("aiModeToggleBackground").objectReferenceValue = aiModeToggleBackground;
            so.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
            so.FindProperty("resultsContainer").objectReferenceValue = resultsContainer;
            so.FindProperty("resultRowTemplate").objectReferenceValue = resultRowTemplate;
            so.FindProperty("answerText").objectReferenceValue = answerText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
