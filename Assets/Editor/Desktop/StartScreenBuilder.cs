using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WindowExe.Desktop.Editor
{
    public static class StartScreenBuilder
    {
        public const string StartScreenScenePath = "Assets/Scenes/StartScreen.unity";
        public const string ShaderPath = "Assets/Shaders/Desktop/CRTMonitorNoise.shader";
        public const string MaterialPath = "Assets/Data/Desktop/CRTMonitorNoiseMat.mat";

        [MenuItem("WINDOW.exe/Start Screen/Setup Start Screen in Current Scene", false, 10)]
        [MenuItem("WINDOW.exe/Desktop/Setup Start Screen in Current Scene", false, 30)]
        [MenuItem("GameObject/UI/WINDOW.exe Start Screen", false, 20)]
        public static void SetupStartScreenInCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");

            var currentScene = EditorSceneManager.GetActiveScene();
            var canvas = CreateStartScreenHierarchy();
            EditorSceneManager.MarkSceneDirty(currentScene);
            Selection.activeGameObject = canvas.gameObject;
            Debug.Log("[StartScreenBuilder] Start Screen UI setup complete in scene: " + currentScene.name);
        }

        [MenuItem("WINDOW.exe/Start Screen/Create New Start Screen Scene", false, 11)]
        [MenuItem("WINDOW.exe/Desktop/Create New Start Screen Scene", false, 31)]
        public static void CreateNewStartScreenScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");

            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            }

            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateStartScreenHierarchy();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(newScene, StartScreenScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[StartScreenBuilder] Created and saved new Start Screen scene at: " + StartScreenScenePath);
        }

        public static Canvas CreateStartScreenHierarchy()
        {
            EnsureEventSystem();
            var material = GetOrCreateNoiseMaterial();

            // 1. Root Canvas
            var canvasGo = new GameObject("StartScreenCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 2. Background Image
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color = new Color(0.12f, 0.14f, 0.2f, 1f); // Fallback dark monitor/room color

            // 3. Monitor Glitch Noise Area
            var noiseGo = new GameObject("MonitorGlitchArea", typeof(RectTransform), typeof(Image));
            noiseGo.transform.SetParent(canvasGo.transform, false);
            var noiseRect = noiseGo.GetComponent<RectTransform>();
            noiseRect.anchorMin = new Vector2(0.5f, 0.5f);
            noiseRect.anchorMax = new Vector2(0.5f, 0.5f);
            noiseRect.pivot = new Vector2(0.5f, 0.5f);
            noiseRect.anchoredPosition = new Vector2(0f, 40f);
            noiseRect.sizeDelta = new Vector2(600f, 450f);
            var noiseImage = noiseGo.GetComponent<Image>();
            noiseImage.color = Color.white;
            if (material != null)
            {
                noiseImage.material = material;
            }

            // 4. Title Image
            var titleGo = new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(600f, 150f);
            var titleImage = titleGo.GetComponent<Image>();
            titleImage.color = new Color(1f, 1f, 1f, 0.9f);

            // 5. Press Any Key Text (TMP)
            var textGo = new GameObject("PressAnyKeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.2f);
            textRect.anchorMax = new Vector2(0.5f, 0.2f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(800f, 80f);

            var tmpText = textGo.GetComponent<TextMeshProUGUI>();
            tmpText.text = "PRESS ANY KEY";
            tmpText.fontSize = 36;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.white;
            GameTypography.Apply(tmpText);

            // 6. StartScreenController
            var controller = canvasGo.AddComponent<StartScreenController>();
            controller.Configure(bgImage, titleImage, tmpText, "Desktop");

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }
            else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null &&
                     eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        public static Material GetOrCreateNoiseMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("WindowExe/CRTMonitorNoise");
            if (shader == null)
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            }

            if (shader == null) return null;

            EnsureFolder("Assets/Data/Desktop");
            mat = new Material(shader);
            mat.name = "CRTMonitorNoiseMat";
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

