using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WindowExe.Desktop.Editor
{
    [InitializeOnLoad]
    public static class GameTypographyEditor
    {
        public const string SettingsPath = "Assets/Data/Desktop/Resources/GameFontSettings.asset";
        private const string SourcePath = "Assets/Art/Fonts/neodgm.ttf";
        private const string TmpPath = "Assets/Data/Desktop/WindowKoreanFont.asset";

        static GameTypographyEditor()
        {
            ObjectFactory.componentWasAdded += ApplyNewComponent;
        }

        private static void ApplyNewComponent(Component component)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!(component is Text) && !(component is TMP_Text)) return;
            if (GameTypography.Settings == null) return;
            Undo.RecordObject(component, "Use game font");
            var changed = component is Text legacy ? GameTypography.Apply(legacy)
                : GameTypography.Apply((TMP_Text)component);
            if (changed) EditorUtility.SetDirty(component);
        }

        public static GameFontSettings EnsureSettings(TMP_FontAsset defaultTmpFont = null)
        {
            const string folder = "Assets/Data/Desktop/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Data/Desktop", "Resources");
            var settings = AssetDatabase.LoadAssetAtPath<GameFontSettings>(SettingsPath);
            if (settings == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
                var tmp = defaultTmpFont != null ? defaultTmpFont : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpPath);
                if (source == null || tmp == null) throw new InvalidOperationException("The source font and TMP font asset must exist before installing game typography.");
                settings = ScriptableObject.CreateInstance<GameFontSettings>();
                settings.Configure(source, tmp);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var tmpSettings = TMP_Settings.instance;
            if (tmpSettings == null) throw new InvalidOperationException("TMP Settings.asset is missing from Resources.");
            if (TMP_Settings.defaultFontAsset != settings.TmpFont)
            {
                TMP_Settings.defaultFontAsset = settings.TmpFont;
                EditorUtility.SetDirty(tmpSettings);
            }
            return settings;
        }

        [MenuItem("WINDOW.exe/Typography/Apply Game Font")]
        public static void ApplyToProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            // Preserve any unsaved scene work; this command only migrates saved scenes.
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before applying game typography.");
            EnsureSettings();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (GameTypography.ApplyHierarchy(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= GameTypography.ApplyHierarchy(root);
                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
