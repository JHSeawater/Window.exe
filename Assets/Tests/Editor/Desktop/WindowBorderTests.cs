using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class WindowBorderTests
    {
        private GameObject windowGo;
        private ImageWindow window;

        [SetUp]
        public void SetUp()
        {
            windowGo = new GameObject("TestImageWindow");
            window = windowGo.AddComponent<ImageWindow>();
            window.InitializeVisuals();
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
        public void BorderRendererIsInitializedOnPrimaryLayerWithCorrectDefaults()
        {
            var border = window.BorderRenderer;
            Assert.IsNotNull(border, "BorderRenderer should be created and cached.");
            Assert.AreEqual(Window.PrimarySortingLayerName, border.sortingLayerName);
            Assert.IsTrue(border.enabled);
            Assert.AreEqual(0.12f, window.BorderThickness, 0.001f);
            Assert.AreEqual(new Color(0.12f, 0.12f, 0.12f, 1f), window.BorderColor);
            Assert.AreEqual(SpriteDrawMode.Sliced, border.drawMode);
        }

        [Test]
        public void BorderSortingOrderIsWindowOrderMinusOne()
        {
            window.ApplySorting(0);
            Assert.AreEqual(-1, window.BorderRenderer.sortingOrder);

            window.ApplySorting(32);
            Assert.AreEqual(31, window.BorderRenderer.sortingOrder);
        }

        [Test]
        public void BorderSizeEncompassesWindowAndTitleBarWithThickness()
        {
            var testSize = new Vector2(5f, 4f);
            window.ApplyGeometry(Vector2.zero, testSize);

            var expectedWidth = testSize.x + window.BorderThickness * 2f;
            var expectedHeight = testSize.y + window.BarHeight + window.BorderThickness * 2f;
            var expectedY = window.BarHeight / 2f;

            Assert.AreEqual(expectedWidth, window.BorderRenderer.size.x, 0.001f);
            Assert.AreEqual(expectedHeight, window.BorderRenderer.size.y, 0.001f);
            Assert.AreEqual(0f, window.BorderRenderer.transform.localPosition.x, 0.001f);
            Assert.AreEqual(expectedY, window.BorderRenderer.transform.localPosition.y, 0.001f);
        }

        [Test]
        public void BorderDoesNotAffectOuterBounds()
        {
            var testSize = new Vector2(4f, 3f);
            window.ApplyGeometry(Vector2.zero, testSize);

            // OuterBounds must remain based strictly on body size + title bar height without border thickness
            var bounds = window.OuterBounds;
            Assert.AreEqual(4f, bounds.width, 0.001f);
            Assert.AreEqual(3f + window.BarHeight, bounds.height, 0.001f);
        }

        [Test]
        public void SetBorderUpdatesAppearanceAndLayout()
        {
            var customColor = new Color(0.2f, 0.3f, 0.4f, 1f);
            window.SetBorder(true, 0.2f, customColor);

            Assert.AreEqual(0.2f, window.BorderThickness, 0.001f);
            Assert.AreEqual(customColor, window.BorderColor);
            Assert.AreEqual(customColor, window.BorderRenderer.color);

            var testSize = window.Size;
            var expectedWidth = testSize.x + 0.2f * 2f;
            var expectedHeight = testSize.y + window.BarHeight + 0.2f * 2f;
            Assert.AreEqual(expectedWidth, window.BorderRenderer.size.x, 0.001f);
            Assert.AreEqual(expectedHeight, window.BorderRenderer.size.y, 0.001f);

            window.SetBorder(false, 0.2f, customColor);
            Assert.IsFalse(window.BorderRenderer.enabled);
        }
    }
}

