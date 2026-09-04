using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    // Mirrors CreatorSearchUISetup's approach (auto-generate + rewire on rerun) but for the
    // much simpler AiQueryPanelBehaviour: one input field, one button, one answer text.
    internal static class AiQueryUISetup
    {
        private static readonly Font UiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Tools/Yobi/Setup AI Query UI")]
        private static void SetupAiQueryUI()
        {
            var existingPanelBehaviour = Object.FindObjectOfType<AiQueryPanelBehaviour>();
            var isNewPanel = existingPanelBehaviour == null;

            GameObject panelGo;
            AiQueryPanelBehaviour panelBehaviour;

            if (existingPanelBehaviour != null)
            {
                panelBehaviour = existingPanelBehaviour;
                panelGo = existingPanelBehaviour.gameObject;
            }
            else
            {
                var canvasGo = EnsureCanvasByName();
                panelGo = CreatePanel(canvasGo.transform);
                panelBehaviour = panelGo.AddComponent<AiQueryPanelBehaviour>();
            }

            EnsureEventSystem();

            DestroyGeneratedChild(panelGo.transform, "QueryRow");
            DestroyGeneratedChild(panelGo.transform, "AnswerText");

            var queryRow = CreateHorizontalRow(panelGo.transform, "QueryRow");
            var inputField = CreateInputField(queryRow.transform, "QueryInputField");
            var askButton = CreateButton(queryRow.transform, "AskButton", "Ask");

            var answerText = CreateAnswerText(panelGo.transform, "AnswerText");

            WireReferences(panelBehaviour, inputField, askButton, answerText);

            EditorUtility.SetDirty(panelGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Selection.activeGameObject = panelGo;
            Debug.Log(isNewPanel
                ? "[AiQueryUISetup] AI Query UI created. Remember to save the scene, then Play and make sure `ollama serve` is running."
                : "[AiQueryUISetup] AI Query UI rebuilt in place on the existing panel. Remember to save the scene.");
        }

        private static GameObject EnsureCanvasByName()
        {
            var existingGo = GameObject.Find("AiQueryCanvas");
            if (existingGo != null && existingGo.GetComponent<Canvas>() != null)
            {
                return existingGo;
            }

            var canvasGo = new GameObject("AiQueryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            return canvasGo;
        }

        private static void EnsureEventSystem()
        {
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

        private static void DestroyGeneratedChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static GameObject CreatePanel(Transform parent)
        {
            var panelGo = new GameObject("AiQueryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(parent, false);

            // Bottom-right, narrower than CreatorSearchPanel's 420px top-left panel - the two
            // used to both claim ~420px in an 800px-wide reference resolution and visibly
            // overlapped in the middle of the screen.
            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 20f);
            rect.sizeDelta = new Vector2(300f, 0f);

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
            placeholder.text = "e.g. 常闇トワ玩過LoL未";
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

        private static Text CreateAnswerText(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
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

        private static void SetupStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void WireReferences(AiQueryPanelBehaviour panel, InputField inputField, Button askButton, Text answerText)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("queryInputField").objectReferenceValue = inputField;
            so.FindProperty("askButton").objectReferenceValue = askButton;
            so.FindProperty("answerText").objectReferenceValue = answerText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
