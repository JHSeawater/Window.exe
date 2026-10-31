using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    /// <summary>Shared font policy for game UI and world-space text. Fonts are asset-owned.</summary>
    public static class GameTypography
    {
        public const string ResourceName = "GameFontSettings";
        public static GameFontSettings Settings => Resources.Load<GameFontSettings>(ResourceName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var settings = Settings;
            if (settings != null && settings.TmpFont != null && TMP_Settings.instance != null)
                TMP_Settings.defaultFontAsset = settings.TmpFont;

            // Re-entering Play Mode without Domain Reload must not add another subscription.
            SceneManager.sceneLoaded -= ApplyScene;
            SceneManager.sceneLoaded += ApplyScene;
        }

        private static void ApplyScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects()) ApplyHierarchy(root);
        }

        public static bool Apply(Text label)
        {
            var font = Settings != null ? Settings.UiFont : null;
            if (label == null || font == null || label.font == font) return false;
            label.font = font;
            return true;
        }

        public static bool Apply(TMP_Text label)
        {
            var font = Settings != null ? Settings.TmpFont : null;
            if (label == null || font == null || label.font == font) return false;
            label.font = font;
            return true;
        }

        public static bool ApplyHierarchy(GameObject root)
        {
            var changed = false;
            foreach (var label in root.GetComponentsInChildren<Text>(true)) changed |= Apply(label);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) changed |= Apply(label);
            return changed;
        }
    }
}
