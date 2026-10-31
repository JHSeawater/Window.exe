using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    public sealed class GameTypographyTests
    {
        [Test]
        public void SharedFontsUseTheProvidedSourceAndAreIncludedInPrefabDependencies()
        {
            var settings = GameTypography.Settings;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/neodgm.ttf");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UiFont, Is.SameAs(source));
            Assert.That(settings.TmpFont.sourceFontFile, Is.SameAs(source));
            Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(settings.TmpFont));
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Desktop" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var label in prefab.GetComponentsInChildren<Text>(true))
                    Assert.That(label.font, Is.SameAs(source), path + "/" + label.name);
                foreach (var label in prefab.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(label.font, Is.SameAs(settings.TmpFont), path + "/" + label.name);
            }
        }

        [Test]
        public void NewEditorTextComponentsReceiveTheGameFont()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("TypographyEditorTest");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var legacyObject = new GameObject("Legacy", typeof(RectTransform));
                legacyObject.transform.SetParent(root.transform);
                var tmpObject = new GameObject("TMP", typeof(RectTransform));
                tmpObject.transform.SetParent(root.transform);
                var legacy = ObjectFactory.AddComponent<Text>(legacyObject);
                var tmp = ObjectFactory.AddComponent<TextMeshProUGUI>(tmpObject);
                Assert.That(legacy.font, Is.SameAs(GameTypography.Settings.UiFont));
                Assert.That(tmp.font, Is.SameAs(GameTypography.Settings.TmpFont));
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [Test]
        public void RuntimeFactoriesUseGameFontsWithoutACanvasFontProvider()
        {
            var root = new GameObject("TypographyFactoryTest", typeof(RectTransform));
            try
            {
                var button = TaskbarUiFactory.CreateButtonTemplate(root.transform);
                var label = button.GetComponentInChildren<Text>();
                Assert.That(label.font, Is.SameAs(GameTypography.Settings.UiFont));
                button.Bind(new TaskbarWindowInfo("font-test", "memo", "아주 긴 한글 제목의 메모장"),
                    false, false, _ => { });
                var windowObject = new GameObject("Memo");
                windowObject.transform.SetParent(root.transform);
                windowObject.AddComponent<NotepadWindow>();
                foreach (var text in windowObject.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(text.font, Is.SameAs(GameTypography.Settings.TmpFont));
                Object.DestroyImmediate(button.gameObject);
                Assert.That(GameTypography.Settings.UiFont, Is.Not.Null,
                    "Removing a text owner must not destroy the shared font asset.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
