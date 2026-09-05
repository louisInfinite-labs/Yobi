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

        [MenuItem("Tools/Yobi/Setup Main Search Bar")]
        private static void SetupMainSearchBar()
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

            var inputField = CreateInputField(barGo.transform, "SearchInputField");
            var backgroundImage = inputField.GetComponent<Image>();

            var resultsContainer = CreateResultsContainer(barGo.transform, out var resultRowTemplate);
            var answerText = CreateAnswerText(barGo.transform, "AnswerText");

            WireReferences(behaviour, inputField, backgroundImage, resultsContainer, resultRowTemplate, answerText);

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
        private static InputField CreateInputField(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 44f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = UiFont;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            SetupStretch(textGo.GetComponent<RectTransform>(), new Vector2(20f, 6f), new Vector2(-20f, -6f));

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholder = placeholderGo.GetComponent<Text>();
            placeholder.font = UiFont;
            placeholder.text = "搜尋創作者,或者問AI一個問題...";
            placeholder.color = new Color(0f, 0f, 0f, 0.4f);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleLeft;
            SetupStretch(placeholderGo.GetComponent<RectTransform>(), new Vector2(20f, 6f), new Vector2(-20f, -6f));

            var inputField = go.GetComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            return inputField;
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

            var addButtonGo = new GameObject("AddButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            addButtonGo.transform.SetParent(row.transform, false);
            addButtonGo.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f, 1f);
            var addButtonLayout = addButtonGo.GetComponent<LayoutElement>();
            addButtonLayout.preferredWidth = 110f;
            addButtonLayout.preferredHeight = 26f;

            var addButtonText = CreateText(addButtonGo.transform, "Text", "加落追蹤");
            addButtonText.color = Color.black;
            addButtonText.alignment = TextAnchor.MiddleCenter;
            SetupStretch(addButtonText.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

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
            Image backgroundImage,
            RectTransform resultsContainer,
            GameObject resultRowTemplate,
            Text answerText)
        {
            var so = new SerializedObject(behaviour);
            so.FindProperty("searchInputField").objectReferenceValue = inputField;
            so.FindProperty("backgroundImage").objectReferenceValue = backgroundImage;
            so.FindProperty("resultsContainer").objectReferenceValue = resultsContainer;
            so.FindProperty("resultRowTemplate").objectReferenceValue = resultRowTemplate;
            so.FindProperty("answerText").objectReferenceValue = answerText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
