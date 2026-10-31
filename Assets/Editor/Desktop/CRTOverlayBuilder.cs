using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WindowExe.Desktop.Editor
{
    public static class CRTOverlayBuilder
    {
        public const string MaterialPath = "Assets/Data/Desktop/Effects/CRTOverlay.mat";
        public const string PrefabPath = "Assets/Prefabs/Desktop/Effects/CRTOverlay.prefab";
        [MenuItem("WINDOW.exe/Desktop/Add CRT Overlay to Open Desktop")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var desktop = UnityEngine.Object.FindAnyObjectByType<DesktopController>();
            if (desktop == null || desktop.gameObject.scene.isDirty) throw new InvalidOperationException("Open and save the desktop first.");
            Install(desktop.gameObject.scene);
            EditorSceneManager.MarkSceneDirty(desktop.gameObject.scene);
            EditorSceneManager.SaveScene(desktop.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        public static GameObject Install(Scene scene)
        {
            EnsureAssets();
            var existing = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CRTOverlay>(true)).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("Multiple CRT effects exist; preserve and review them before upgrading.");
            if (existing.Length == 1) return existing[0].transform.root.gameObject;
            return (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        }
        public static void EnsureAssets()
        {
            EnsureFolder("Assets/Data/Desktop/Effects"); EnsureFolder("Assets/Prefabs/Desktop/Effects");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("WindowExe/CRTOverlay");
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("A supported CRT shader is required.");
                material = new Material(shader); material.SetFloat("_RollStrength", .000625f);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            var root = new GameObject("CRTOverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            try
            {
                root.layer = 5;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 999;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var screen = new GameObject("CRTOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                screen.layer = 5; screen.transform.SetParent(root.transform, false);
                var rect = (RectTransform)screen.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = screen.GetComponent<RawImage>(); image.enabled = false; image.raycastTarget = false;
                image.color = Color.white; image.uvRect = new Rect(0, 0, 1, 1); image.material = material;
                screen.AddComponent<CRTOverlay>().baseMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
