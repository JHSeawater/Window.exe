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
            Assert.AreEqual(0.06f, window.BorderThickness, 0.001f);
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

        [Test]
        public void BreakingRightBorderReducesWidthAndShiftsLeftWithDebrisAndShake()
        {
            var testSize = new Vector2(4f, 3f);
            window.ApplyGeometry(Vector2.zero, testSize);
            float t = window.BorderThickness;

            Assert.IsFalse(window.IsBorderBroken);
            Assert.IsTrue(window.BreakRightBorder());
            Assert.IsTrue(window.IsBorderBroken);

            // 가로 두께가 2t -> 1t로 줄고 중심이 -t/2로 이동하여 우측 끝이 +size.x/2와 정확히 일치
            Assert.AreEqual(testSize.x + t, window.BorderRenderer.size.x, 0.001f);
            Assert.AreEqual(-t * 0.5f, window.BorderRenderer.transform.localPosition.x, 0.001f);
            float rightEdge = window.BorderRenderer.transform.localPosition.x + window.BorderRenderer.size.x * 0.5f;
            Assert.AreEqual(testSize.x * 0.5f, rightEdge, 0.001f);

            // 탈락 파편 및 흔들림 연출 활성화 확인
            Assert.IsTrue(window.HasActiveDebris);
            Assert.IsTrue(window.IsShakeActive);

            window.ManualTick(0.1f);
            Assert.IsTrue(window.HasActiveDebris);

            window.ManualTick(1.0f);
            Assert.IsFalse(window.HasActiveDebris);
            Assert.IsFalse(window.IsShakeActive);
        }

        [Test]
        public void BreakingRightBorderBreaksIntoMultipleSegmentsCollapsingInward()
        {
            var testSize = new Vector2(4f, 3f);
            window.ApplyGeometry(Vector2.zero, testSize);
            float t = window.BorderThickness;
            float initialRightEdgeX = testSize.x * 0.5f + t * 0.5f;

            Assert.IsTrue(window.BreakRightBorder());
            Assert.GreaterOrEqual(window.DebrisPieceCount, 5, "테두리가 여러 개의 선분 조각으로 분할되어야 합니다.");

            var positionsBefore = new System.Collections.Generic.List<Vector2>();
            window.GetDebrisPositions(positionsBefore);
            Assert.AreEqual(window.DebrisPieceCount, positionsBefore.Count);

            // 1. 빠른 초기 반응: 모든 파편이 창 안쪽(왼쪽, -X)으로 속도감 있게 진입하는지 검증
            window.ManualTick(0.08f);

            var positionsEarly = new System.Collections.Generic.List<Vector2>();
            window.GetDebrisPositions(positionsEarly);

            for (int i = 0; i < positionsEarly.Count; i++)
            {
                Assert.Less(positionsEarly[i].x, initialRightEdgeX, $"파편 {i}번이 창 안쪽(왼쪽)으로 이동해야 합니다.");
            }

            // 2. 중력 낙하: 초기 상향 확산 후 시간이 지나면 모든 파편이 초기 높이 아래로 무너져 내리는지 검증
            window.ManualTick(0.25f);

            var positionsLate = new System.Collections.Generic.List<Vector2>();
            window.GetDebrisPositions(positionsLate);

            for (int i = 0; i < positionsLate.Count; i++)
            {
                Assert.Less(positionsLate[i].y, positionsBefore[i].y, $"파편 {i}번이 중력을 받아 초기 위치보다 아래로 떨어져야 합니다.");
            }
        }

        [Test]
        public void BreakingRightBorderActivatesBrokenOverlayAndPlaysSeparateBreakSound()
        {
            window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));

            Assert.IsFalse(window.IsBrokenOverlayActive);
            Assert.AreEqual(0, window.BreakSoundPlayCount);

            Assert.IsTrue(window.BreakRightBorder());

            // 1. 깨진 텍스처 오버레이 활성화 및 듀얼 레이어 동기화 확인
            Assert.IsTrue(window.IsBrokenOverlayActive);
            Assert.IsNotNull(window.BrokenOverlayFront);
            Assert.IsTrue(window.BrokenOverlayFront.gameObject.activeSelf);
            Assert.IsNotNull(window.BrokenOverlayBack);
            Assert.IsTrue(window.BrokenOverlayBack.gameObject.activeSelf);

            // 2. 깨지는 효과음이 별도로 재생되었는지 확인
            Assert.AreEqual(1, window.BreakSoundPlayCount);
            Assert.IsNotNull(window.LastPlayedBreakClip);

            // 3. 커스텀 깨진 스프라이트 교체 지원 확인
            var customTex = new Texture2D(2, 2);
            var customSprite = Sprite.Create(customTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            try
            {
                window.SetBrokenOverlaySprite(customSprite);
                Assert.AreEqual(customSprite, window.BrokenOverlayFront.sprite);
                Assert.AreEqual(customSprite, window.BrokenOverlayBack.sprite);
            }
            finally
            {
                Object.DestroyImmediate(customSprite);
                Object.DestroyImmediate(customTex);
            }
        }

        [Test]
        public void BrokenKeyWindowAllowsDraggingKeyImageOutAndDroppingIntoRecycleBinTriggersEvent()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);

                // 깨지기 전에는 드래그 불가
                Assert.IsFalse(window.BeginKeyDrag(Vector2.zero));

                // 우측 테두리 파손 후 드래그 시작 가능
                Assert.IsTrue(window.BreakRightBorder());
                Assert.IsTrue(window.BeginKeyDrag(Vector2.zero));
                Assert.IsTrue(window.IsDraggingImage);

                // 창 밖(오른쪽 아래)으로 드래그 이동
                Vector2 outsidePos = new Vector2(6f, -3f);
                window.UpdateKeyDrag(outsidePos);
                Assert.IsTrue(window.IsKeyOutsideWindow);
                Assert.AreEqual(outsidePos.x, window.DraggedKeyWorldPosition.x, 0.001f);
                Assert.AreEqual(outsidePos.y, window.DraggedKeyWorldPosition.y, 0.001f);

                // 쓰레기통 위치를 (10, -4) 주변으로 설정하고 그곳에 드롭 시 이벤트 발생 확인
                window.OverrideRecycleBinWorldRect = new Rect(9f, -5f, 2f, 2f);
                bool eventTriggered = false;
                window.OnKeyDroppedToRecycleBin += () => eventTriggered = true;

                Assert.IsTrue(window.EndKeyDrag(new Vector2(10f, -4f)));
                Assert.IsTrue(eventTriggered);
                Assert.IsTrue(window.IsKeyConsumed);
                Assert.IsFalse(window.IsDraggingImage);
            }
            finally
            {
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }
    }
}

