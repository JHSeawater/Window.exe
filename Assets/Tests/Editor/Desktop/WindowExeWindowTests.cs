using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class WindowExeWindowTests
    {
        private GameObject windowGo;
        private WindowExeWindow windowExe;

        [SetUp]
        public void SetUp()
        {
            windowGo = new GameObject("Window_WINDOW.exe");
            windowExe = windowGo.AddComponent<WindowExeWindow>();
            windowExe.InitializeVisuals();
        }

        [TearDown]
        public void TearDown()
        {
            if (windowGo != null)
            {
                Object.DestroyImmediate(windowGo);
            }
        }

        [Test]
        public void ViewportRendererIsConfiguredInPrimaryLayer()
        {
            var renderer = windowExe.ViewportRenderer;
            Assert.IsNotNull(renderer);
            Assert.AreEqual(Window.PrimarySortingLayerName, renderer.sortingLayerName);
            Assert.AreEqual(0, renderer.sortingOrder);
            Assert.AreEqual(SpriteDrawMode.Sliced, renderer.drawMode);
            Assert.IsNotNull(renderer.sprite);
        }

        [Test]
        public void SettingMaterialUpdatesViewportSharedMaterial()
        {
            var testShader = Shader.Find("Sprites/Default");
            var testMat = new Material(testShader);

            windowExe.SetMaterial(testMat);
            Assert.AreEqual(testMat, windowExe.ViewportRenderer.sharedMaterial);

            Object.DestroyImmediate(testMat);
        }

        [Test]
        public void ResizingWindowUpdatesViewportSize()
        {
            var newSize = new Vector2(6.5f, 4.5f);
            windowExe.Resize(newSize);

            Assert.AreEqual(newSize, windowExe.Size);
            Assert.AreEqual(newSize.x, windowExe.ViewportRenderer.size.x, 0.001f);
            Assert.AreEqual(newSize.y, windowExe.ViewportRenderer.size.y, 0.001f);
        }

        [Test]
        public void HasBasicWindowFrameElements()
        {
            // TitleBar 확인
            var titleBar = windowGo.GetComponentInChildren<WindowTitleBar>();
            Assert.IsNotNull(titleBar);

            // ResizeHandle 확인
            var resizeHandle = windowGo.GetComponentInChildren<WindowResizeHandle>();
            Assert.IsNotNull(resizeHandle);
            Assert.IsTrue(resizeHandle.gameObject.activeSelf);

            // 8방향 리사이즈 핸들 확인
            var handles = resizeHandle.GetComponentsInChildren<SingleResizeHandle>();
            Assert.AreEqual(8, handles.Length);
        }
    }
}

