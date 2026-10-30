using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WindowExe.Desktop.Editor
{
    public static class DesktopSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Desktop.unity";
        public const string PrefabPath = "Assets/Prefabs/Desktop/DesktopIcon.prefab";

        [MenuItem("WINDOW.exe/Desktop/Add Selection Box to Open Desktop")]
        public static void AddSelectionBoxToOpenDesktop()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var desktop = UnityEngine.Object.FindAnyObjectByType<DesktopController>();
            if (desktop == null || desktop.IconArea == null)
                throw new InvalidOperationException("Open a desktop scene first.");
            var scene = desktop.gameObject.scene;
            if (scene.isDirty) throw new InvalidOperationException("Save modified scenes before updating the desktop.");
            var box = desktop.GetComponentInChildren<DesktopSelectionBox>(true);
            if (box == null) box = DesktopSelectionBox.Create(desktop.IconArea);
            desktop.ConfigureSelectionBox(box);
            EditorUtility.SetDirty(desktop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("WINDOW.exe/Desktop/Create or Open Desktop")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save modified scenes before opening the desktop.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs/Desktop");
            EnsureFolder("Assets/Data/Desktop");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            var canvasObject = new GameObject("DesktopCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = 5;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var desktop = canvasObject.AddComponent<DesktopController>();
            var background = Rect("Wallpaper", canvasObject.transform, Vector2.zero, Vector2.zero);
            Stretch(background);
            background.gameObject.AddComponent<RawImage>().texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Screen/Desktop.png");
            background.gameObject.AddComponent<DesktopBackground>().Configure(desktop);

            var area = Rect("IconArea", canvasObject.transform, Vector2.zero, Vector2.zero);
            Stretch(area, 20);
            var windows = Rect("WindowLayer", canvasObject.transform, Vector2.zero, Vector2.zero);
            Stretch(windows);
            desktop.Configure(area, windows);
            desktop.ConfigureSelectionBox(DesktopSelectionBox.Create(area));
            canvasObject.AddComponent<DesktopFont>();
            canvasObject.AddComponent<DesktopOpenRequestProbe>().Configure(desktop);

            var prefab = GetOrCreatePrefab();
            var definitions = new[]
            {
                Item("recycle-bin", "휴지통", "Assets/Art/Icon/bin.png"),
                Item("memo", "메모.txt", "Assets/Art/Icon/Icon_Memo.png"),
                Item("photos", "사진", "Assets/Art/Icon/Album.png"),
                Item("internet", "인터넷", "Assets/Art/Icon/internet.png")
            };
            for (var i = 0; i < definitions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, area);
                instance.name = "Icon_" + definitions[i].ItemId;
                var icon = instance.GetComponent<DesktopIconView>();
                icon.Configure(desktop, definitions[i], instance.GetComponent<Image>(),
                    instance.transform.Find("Image").GetComponent<Image>(),
                    instance.transform.Find("Name").GetComponent<Text>());
                icon.Rect.anchoredPosition = new Vector2(8, -8 - i * 164);
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<EventSystem>().pixelDragThreshold = 8;
            Canvas.ForceUpdateCanvases();
            desktop.EnsureValidLayout();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = canvasObject;
        }

        private static DesktopItemDefinition Item(string id, string title, string imagePath)
        {
            var path = "Assets/Data/Desktop/" + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<DesktopItemDefinition>(path);
            if (existing != null) return existing;
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            var sprites = AssetDatabase.LoadAllAssetsAtPath(imagePath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("Missing icon sprite: " + imagePath);
            var definition = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            definition.Configure(id, title, sprites[0]);
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static GameObject GetOrCreatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var root = Rect("DesktopIcon", null, Vector2.zero, new Vector2(144, 152));
            var selection = root.gameObject.AddComponent<Image>();
            selection.color = Color.clear;
            var picture = Rect("Image", root, new Vector2(8, -2), new Vector2(128, 120)).gameObject.AddComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            var label = Rect("Name", root, new Vector2(2, -122), new Vector2(140, 28)).gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = false;
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            root.gameObject.AddComponent<DesktopIconView>().Configure(null, null, selection, picture, label);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
