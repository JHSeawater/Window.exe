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
            ImageWindow.ResetGlobalExtractionState();
            if (windowGo != null)
            {
                Object.DestroyImmediate(windowGo);
            }
        }

        [Test]
        public void LockWarningAcceptsOnlyPadlockAndKeepsUnlockAfterRefresh()
        {
            var go = new GameObject("LockWarningTest");
            try
            {
                var warning = go.AddComponent<LockWarningWindow>();
                var lockSprite = RepairWindow.GetFallbackInitialSprite();
                var unlockSprite = RepairWindow.GetFallbackSecondSprite();
                warning.InitialSprite = lockSprite;
                warning.SecondSprite = unlockSprite;
                warning.InitializeVisuals();
                warning.ApplyGeometry(Vector2.zero, new Vector2(14, 5.2f));
                var textPoint = warning.ImageToWorld(new Vector2(.55f, .55f));
                Assert.IsFalse(warning.CanUseKeyAt(textPoint), "Message text must not accept a key.");
                Assert.IsNull(RepairWindow.FindRepairWindowAt(textPoint));
                Assert.AreSame(warning, RepairWindow.FindRepairWindowAt(warning.KeyTargetCenter));
                Assert.AreEqual(0, warning.BarHeight);
                Assert.IsFalse(warning.transform.Find("TitleBar").gameObject.activeSelf);
                int events = 0;
                warning.OnKeyUsed += () => events++;
                Assert.IsTrue(warning.TryUseKey());
                Assert.AreEqual(1, events);
                Assert.IsFalse(warning.TryUseKey());
                warning.InitializeVisuals();
                Assert.AreSame(unlockSprite, warning.FrontImageRenderer.sprite);
                Assert.AreSame(unlockSprite, warning.BackImageRenderer.sprite);
                Assert.IsFalse(warning.CanUseKeyAt(warning.KeyTargetCenter));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LockWarningPrefabRevealsBothStagesWithoutChangingFrontOrHitTargets()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Desktop/Windows/LockWarningWindow.prefab");
            var go = Object.Instantiate(prefab);
            try
            {
                var warning = go.GetComponent<LockWarningWindow>();
                warning.InitializeVisuals();
                warning.ApplyGeometry(new Vector2(1, -.5f), new Vector2(14, 5.2f));
                void AssertImages(string name)
                {
                    Assert.AreEqual("Assets/Art/Warning/" + name + ".png",
                        UnityEditor.AssetDatabase.GetAssetPath(warning.FrontImageRenderer.sprite));
                    Assert.AreEqual("Assets/Art/Warning/" + name + "-rusted-bloody.png",
                        UnityEditor.AssetDatabase.GetAssetPath(warning.BackImageRenderer.sprite));
                    Assert.AreEqual(warning.FrontImageRenderer.sprite.rect, warning.BackImageRenderer.sprite.rect);
                    Assert.AreEqual(warning.FrontImageRenderer.sprite.pivot, warning.BackImageRenderer.sprite.pivot);
                    Assert.Less(Vector3.Distance(warning.FrontImageRenderer.bounds.center, warning.BackImageRenderer.bounds.center), .0001f);
                    Assert.Less(Vector3.Distance(warning.FrontImageRenderer.bounds.size, warning.BackImageRenderer.bounds.size), .0001f);
                }
                AssertImages("Lock");
                Assert.IsTrue(warning.CanUseKeyAt(warning.KeyTargetCenter));
                Assert.AreEqual(2, warning.ButtonAt(warning.ImageToWorld(new Vector2(.49f, .31f))));
                Assert.IsTrue(warning.TryUseKey());
                AssertImages("Unlock");
                warning.InitializeVisuals();
                warning.ApplyGeometry(new Vector2(-1, 1), new Vector2(9, 4));
                AssertImages("Unlock");
                Assert.AreEqual(2, warning.ButtonAt(warning.ImageToWorld(new Vector2(.465f, .23f))));
                Assert.AreEqual(2, warning.ButtonAt(warning.ImageToWorld(new Vector2(.885f, .805f))));
                Assert.IsFalse(warning.TryUseKey());
            }
            finally { Object.DestroyImmediate(go); }
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
            Assert.IsTrue(window.IsBothBordersBroken);

            // 양쪽 테두리가 모두 제거되어 좌우 테두리가 본문 크기(testSize.x)와 정확히 일치
            Assert.AreEqual(testSize.x, window.BorderRenderer.size.x, 0.001f);
            Assert.AreEqual(0f, window.BorderRenderer.transform.localPosition.x, 0.001f);
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
        public void BreakingRightBorderKeepsOverlayEmptyByDefaultAndOnlyShowsWhenSpriteAssigned()
        {
            window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));

            Assert.IsFalse(window.IsBrokenOverlayActive);
            Assert.AreEqual(0, window.BreakSoundPlayCount);

            Assert.IsTrue(window.BreakRightBorder());

            // 1. 기본적으로 오버레이 스프라이트가 없으므로 깨져도 오버레이는 비활성화 상태 유지
            Assert.IsFalse(window.IsBrokenOverlayActive, "기본 크랙 오버레이는 비워져 있어야 합니다.");

            // 2. 깨지는 효과음이 별도로 재생되었는지 확인
            Assert.AreEqual(1, window.BreakSoundPlayCount);
            Assert.IsNotNull(window.LastPlayedBreakClip);

            // 3. 커스텀 깨진 스프라이트 할당 시 활성화 확인
            var customTex = new Texture2D(2, 2);
            var customSprite = Sprite.Create(customTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            try
            {
                window.SetBrokenOverlaySprite(customSprite);
                Assert.IsTrue(window.IsBrokenOverlayActive);
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
        public void BrokenKeyWindowAllowsDraggingKeyImageOutAndDroppingIntoRepairWindowReplacesImage()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            GameObject repairGo = new GameObject("RepairWindow");
            var repairWindow = repairGo.AddComponent<RepairWindow>();

            try
            {
                repairWindow.ConsumeKeyOnUse = true;
                RepairWindow.OverrideActiveRepairWindow = repairWindow;
                RepairWindow.OverrideRepairWindowWorldRect = new Rect(9f, -5f, 2f, 2f);

                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = false;

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

                // 1. 기존 휴지통 영역(허공)에 드롭 시에는 소비되지 않음 (휴지통 연결점 삭제 검증)
                window.OverrideRecycleBinWorldRect = new Rect(3f, -5f, 2f, 2f);
                Assert.IsFalse(window.EndKeyDrag(new Vector2(4f, -4f)));
                Assert.IsFalse(window.IsKeyConsumed);

                // 2. 복원 창 위치 (10, -4)에 드롭 시 복원 이벤트 발생 및 2단계 이미지 교체 확인
                Assert.IsTrue(window.BeginKeyDrag(new Vector2(4f, -4f)));
                bool eventTriggered = false;
                repairWindow.OnKeyUsed += () => eventTriggered = true;

                Assert.IsTrue(window.EndKeyDrag(new Vector2(10f, -4f)));
                Assert.IsTrue(eventTriggered, "복원 창에 드롭 시 OnKeyUsed 이벤트가 호출되어야 함");
                Assert.AreEqual(1, repairWindow.CurrentStage, "복원 창의 Stage가 1로 전환되어야 함");
                Assert.IsTrue(window.IsKeyConsumed, "consumeKeyOnUse == true이므로 열쇠가 소비되어야 함");
                Assert.IsFalse(window.IsDraggingImage);
            }
            finally
            {
                RepairWindow.OverrideActiveRepairWindow = null;
                RepairWindow.OverrideRepairWindowWorldRect = null;
                Object.DestroyImmediate(repairGo);
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void KeyDragRequiresBeingFullyInsideWindowExeWhenOptionEnabled()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = true;

                Assert.IsTrue(window.BreakRightBorder());

                // 1. WINDOW.exe가 비추지 않으면 드래그 시작 실패
                ImageWindow.OverrideWindowExeBounds = null;
                Assert.IsFalse(window.BeginKeyDrag(Vector2.zero));

                // 2. WINDOW.exe가 온전히 포함하지 않고 일부만 걸치면 드래그 실패
                ImageWindow.OverrideWindowExeBounds = new Rect(0.5f, 0.5f, 1f, 1f);
                Assert.IsFalse(window.BeginKeyDrag(Vector2.zero));

                // 3. WINDOW.exe가 열쇠 전체 영역을 온전히 포함하면 드래그 성공
                ImageWindow.OverrideWindowExeBounds = new Rect(-5f, -5f, 10f, 10f);
                Assert.IsTrue(window.BeginKeyDrag(Vector2.zero));
                Assert.IsTrue(window.IsDraggingImage);
            }
            finally
            {
                ImageWindow.OverrideWindowExeBounds = null;
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void ExtractedKeyPersistsInWorldAsEntityWhenWindowCloses()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = false;

                Assert.IsTrue(window.BreakRightBorder());
                Assert.IsTrue(window.BeginKeyDrag(Vector2.zero));

                // 창 밖으로 드래그
                Vector2 outsidePos = new Vector2(6f, -3f);
                window.UpdateKeyDrag(outsidePos);
                window.EndKeyDrag(outsidePos);
                Assert.IsTrue(window.IsKeyOutsideWindow);
                Assert.IsTrue(ImageWindow.IsKeyGloballyExtracted);

                // 열쇠 창을 닫음 (Destroy)
                Object.DestroyImmediate(windowGo);
                windowGo = null;

                // 열쇠 창이 닫혀도 독립 월드 엔티티로 승격되어 유지됨
                var worldEntity = ImageWindow.ActiveWorldKeyEntity;
                Assert.IsNotNull(worldEntity, "창 밖으로 꺼낸 열쇠는 창이 닫혀도 월드 엔티티로 유지되어야 함");
                Assert.AreEqual(outsidePos.x, worldEntity.transform.position.x, 0.001f);
                Assert.AreEqual(outsidePos.y, worldEntity.transform.position.y, 0.001f);

                // 월드 엔티티를 복원 창에 넣으면 이벤트 발생 및 소비됨
                GameObject repairGo = new GameObject("RepairWindow");
                var repairWindow = repairGo.AddComponent<RepairWindow>();
                RepairWindow.OverrideActiveRepairWindow = repairWindow;
                RepairWindow.OverrideRepairWindowWorldRect = new Rect(9f, -5f, 2f, 2f);

                bool globalEventTriggered = false;
                repairWindow.OnKeyUsed += () => globalEventTriggered = true;

                try
                {
                    Assert.IsTrue(worldEntity.BeginDrag(outsidePos));
                    Assert.IsTrue(worldEntity.EndDrag(new Vector2(10f, -4f)));
                    Assert.IsTrue(globalEventTriggered);
                    Assert.AreEqual(1, repairWindow.CurrentStage);
                }
                finally
                {
                    RepairWindow.OverrideActiveRepairWindow = null;
                    RepairWindow.OverrideRepairWindowWorldRect = null;
                    Object.DestroyImmediate(repairGo);
                }
            }
            finally
            {
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void RepairWindowRetainsKeyAtDropPositionWhenConsumeKeyOnUseIsFalse()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            GameObject repairGo = new GameObject("RepairWindow");
            var repairWindow = repairGo.AddComponent<RepairWindow>();

            try
            {
                // 열쇠 미소모 옵션(consumeKeyOnUse = false)
                repairWindow.ConsumeKeyOnUse = false;
                RepairWindow.OverrideActiveRepairWindow = repairWindow;
                RepairWindow.OverrideRepairWindowWorldRect = new Rect(9f, -5f, 2f, 2f);

                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = false;

                Assert.IsTrue(window.BreakRightBorder());
                Assert.IsTrue(window.BeginKeyDrag(Vector2.zero));

                Vector2 dropPos = new Vector2(10f, -4f);
                Assert.IsTrue(window.EndKeyDrag(dropPos));

                // 복원은 성공하여 이미지가 교체되었지만, 열쇠는 소모되지 않고 놓인 위치에 그대로 유지되어야 함
                Assert.AreEqual(1, repairWindow.CurrentStage);
                Assert.IsFalse(window.IsKeyConsumed, "consumeKeyOnUse == false이므로 열쇠가 소모되지 않아야 함");
                Assert.IsTrue(window.IsKeyOutsideWindow);
                Assert.IsTrue(ImageWindow.IsKeyGloballyExtracted);

                var worldEntity = ImageWindow.ActiveWorldKeyEntity;
                Assert.IsNotNull(worldEntity, "열쇠는 놓인 위치에 독립 엔티티로 유지되어야 함");
                Assert.AreEqual(dropPos.x, worldEntity.transform.position.x, 0.001f);
                Assert.AreEqual(dropPos.y, worldEntity.transform.position.y, 0.001f);
            }
            finally
            {
                RepairWindow.OverrideActiveRepairWindow = null;
                RepairWindow.OverrideRepairWindowWorldRect = null;
                Object.DestroyImmediate(repairGo);
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void FolderWindowDoubleClickExecutesTargetRepairWindow()
        {
            var wmGo = new GameObject("WindowManager");
            var wm = wmGo.AddComponent<WindowManager>();

            var repairPrefabGo = new GameObject("RepairPrefab");
            var repairPrefab = repairPrefabGo.AddComponent<RepairWindow>();
            wm.RegisterPrefab("repair", repairPrefab);

            var folderGo = new GameObject("FolderWindow");
            var folder = folderGo.AddComponent<FolderWindow>();
            folder.TargetExecutableWindowId = "repair";
            folder.Attach(wm, "folder");

            try
            {
                bool executedEvent = false;
                folder.OnExecuted += () => executedEvent = true;

                // 더블클릭 실행 시뮬레이션
                Assert.IsTrue(folder.SimulateDoubleClick());
                Assert.IsTrue(executedEvent, "FolderWindow 앱 아이콘 더블클릭 시 OnExecuted 이벤트가 호출되어야 함");
                Assert.IsTrue(wm.IsWindowOpen("repair"), "FolderWindow 실행 시 대상 창('repair')이 열려야 함");
            }
            finally
            {
                Object.DestroyImmediate(folderGo);
                Object.DestroyImmediate(repairPrefabGo);
                Object.DestroyImmediate(wmGo);
            }
        }

        [Test]
        public void KeyDragAllowedWhenKeyObjectInsideWindowExeEvenIfWindowPartiallyOutside()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                // 열쇠 창이 (3, 0), 크기 4x3 -> 바운드는 x:[1, 5], y:[-1.5, 1.5]
                window.ApplyGeometry(new Vector2(3f, 0f), new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = true;

                Assert.IsTrue(window.BreakRightBorder());

                // WINDOW.exe가 x:[2, 4], y:[-2, 2]에 위치:
                // 열쇠 중심점 (3, 0)은 WINDOW.exe 내부에 있지만 창의 테두리 일부(x: 1~2, x: 4~5)는 바깥에 걸쳐 있음
                ImageWindow.OverrideWindowExeBounds = new Rect(2f, -2f, 2f, 4f);

                // 창 전체가 아닌 열쇠 오브젝트(중심점)가 WINDOW.exe 안에 온전히 포함되므로 드래그 성공해야 함
                Assert.IsTrue(window.BeginKeyDrag(new Vector2(3f, 0f)));
                Assert.IsTrue(window.IsDraggingImage);
            }
            finally
            {
                ImageWindow.OverrideWindowExeBounds = null;
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void PreventDraggingKeyOutsideWindowExeClampsMovementInsideWindowExeBounds()
        {
            var tex = new Texture2D(2, 2);
            var keySprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                window.ApplyGeometry(Vector2.zero, new Vector2(4f, 3f));
                window.SetPrimarySprite(keySprite);
                window.RequireWindowExeToExtractFromWindow = false;
                window.PreventDraggingKeyOutsideWindowExe = true;

                // WINDOW.exe 영역: [-3, -3] ~ [3, 3]
                ImageWindow.OverrideWindowExeBounds = new Rect(-3f, -3f, 6f, 6f);

                Assert.IsTrue(window.BreakRightBorder());
                Assert.IsTrue(window.BeginKeyDrag(Vector2.zero));

                // 윈도우 밖인 (15, 0)으로 드래그 시도
                window.UpdateKeyDrag(new Vector2(15f, 0f));

                // WINDOW.exe 경계(xMax = 3.0f) 안으로 클램핑되어 15까지 나가지 못해야 함
                Assert.LessOrEqual(window.DraggedKeyWorldPosition.x, 3.0f);
                Assert.GreaterOrEqual(window.DraggedKeyWorldPosition.x, 2.5f);
            }
            finally
            {
                ImageWindow.OverrideWindowExeBounds = null;
                Object.DestroyImmediate(keySprite);
                Object.DestroyImmediate(tex);
            }
        }
    }
}

