using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yobi.Presentation;

namespace Yobi.EditorTools
{
    internal static class RoomBackgroundUISetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Yobi/Setup Room Background")]
        private static void SetupRoomBackground()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var existing = Object.FindFirstObjectByType<RoomBackgroundBehaviour>();
            if (existing != null)
            {
                Debug.Log("[RoomBackgroundUISetup] Already present.");
                return;
            }

            var canvasGo = new GameObject("RoomBackgroundCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Well behind every other Canvas (which default to 0) so other UI always draws on
            // top of the wallpaper rather than being hidden by it.
            canvas.sortingOrder = -100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);

            var imageGo = new GameObject("RoomBackgroundImage", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var imageRect = imageGo.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            var image = imageGo.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
            // Hidden until DesktopCompanionWindowBehaviour enables it for Room mode - starting
            // visible would show a blank white rect over the DesktopMate transparent overlay.
            image.enabled = false;

            var behaviour = canvasGo.AddComponent<RoomBackgroundBehaviour>();
            var so = new SerializedObject(behaviour);
            so.FindProperty("backgroundImage").objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(canvasGo);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasGo;
            Debug.Log($"[RoomBackgroundUISetup] Room background created. SaveScene returned {saved}.");
        }
    }
}
