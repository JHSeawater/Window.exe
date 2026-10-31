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
        public void SharedTmpSettingsResolveLineBreakingStyleAndShaderDependencies()
        {
            const string resources = "Assets/Data/Desktop/Resources/";
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(resources + "TMP Settings.asset");
            Assert.That(TMP_Settings.instance, Is.SameAs(settings));
            Assert.That(TMP_Settings.leadingCharacters, Is.Not.Null, "The leading-character GUID must resolve.");
            Assert.That(TMP_Settings.followingCharacters, Is.Not.Null, "The following-character GUID must resolve.");
            Assert.That(AssetDatabase.GetAssetPath(TMP_Settings.leadingCharacters),
                Is.EqualTo(resources + "LineBreaking Leading Characters.txt"));
            Assert.That(AssetDatabase.GetAssetPath(TMP_Settings.followingCharacters),
                Is.EqualTo(resources + "LineBreaking Following Characters.txt"));
            Assert.That(TMP_Settings.defaultStyleSheet, Is.Not.Null, "The style-sheet GUID must resolve.");
            Assert.That(AssetDatabase.GetAssetPath(TMP_Settings.defaultStyleSheet),
                Is.EqualTo(resources + "WindowDefaultStyle.asset"));
            TMP_Settings.LoadLinebreakingRules();
            Assert.That(TMP_Settings.linebreakingRules.leadingCharacters, Is.Not.Empty);
            Assert.That(TMP_Settings.linebreakingRules.followingCharacters, Is.Not.Empty);

            var shader = GameTypography.Settings.TmpFont.material.shader;
            Assert.That(shader, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(shader),
                Is.EqualTo("Assets/Shaders/Desktop/TMP_SDF-Mobile.shader"),
                "The shared font must use the tracked shader, not an error or local-only shader.");
            Assert.That(shader.isSupported, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WrappedKoreanTextBuildsVisibleGeometry(bool useUiText)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("KoreanMeshRegression", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                if (useUiText) root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject("Text", typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                TMP_Text label = useUiText ? (TMP_Text)textObject.AddComponent<TextMeshProUGUI>()
                    : textObject.AddComponent<TextMeshPro>();
                GameTypography.Apply(label);
                label.fontSize = 24;
                label.rectTransform.sizeDelta = useUiText ? new Vector2(120, 200) : new Vector2(2, 5);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.text = "한글 줄바꿈과 문장부호를 확인합니다. 「창 너머의 기록」을 읽고 다음 단서를 찾아보세요.";
                label.ForceMeshUpdate(true, true);
                Assert.That(label.textInfo.characterCount, Is.EqualTo(label.text.Length));
                Assert.That(label.textInfo.lineCount, Is.GreaterThan(1));
                Assert.That(label.textInfo.meshInfo[0].vertexCount, Is.GreaterThan(0));
                Assert.That(label.fontSharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

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
