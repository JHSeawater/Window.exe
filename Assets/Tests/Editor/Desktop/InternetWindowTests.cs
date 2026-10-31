using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class InternetWindowTests
    {
        private GameObject managerGo;
        private WindowManager manager;
        private GameObject wifiGo;
        private WifiConnection wifi;
        private GameObject internetPrefabGo;
        private GameObject windowExePrefabGo;
        private InternetWindow internet;

        [SetUp]
        public void SetUp()
        {
            wifiGo = new GameObject("WifiConnection");
            wifi = wifiGo.AddComponent<WifiConnection>();
            wifi.SetConnected(false);

            managerGo = new GameObject("WindowManager");
            manager = managerGo.AddComponent<WindowManager>();

            internetPrefabGo = new GameObject("InternetPrefab");
            internetPrefabGo.SetActive(false);
            var internetPrefab = internetPrefabGo.AddComponent<InternetWindow>();

            windowExePrefabGo = new GameObject("WindowExePrefab");
            windowExePrefabGo.SetActive(false);
            var windowExePrefab = windowExePrefabGo.AddComponent<WindowExeWindow>();

            manager.RegisterPrefab("internet", internetPrefab);
            manager.RegisterPrefab("window", windowExePrefab);

            internet = manager.Open("internet") as InternetWindow;
            Assert.IsNotNull(internet);
        }

        [TearDown]
        public void TearDown()
        {
            if (manager != null)
                manager.ResetWindows();

            if (internetPrefabGo != null)
                Object.DestroyImmediate(internetPrefabGo);

            if (windowExePrefabGo != null)
                Object.DestroyImmediate(windowExePrefabGo);

            if (managerGo != null)
                Object.DestroyImmediate(managerGo);

            if (wifiGo != null)
                Object.DestroyImmediate(wifiGo);
        }

        [Test]
        public void StartsInPhase1WithUniformFrontAndDistinctBackDisks()
        {
            Assert.IsFalse(internet.IsOnline);
            Assert.AreEqual(InternetWindow.InternetPhase.NetworkPuzzle, internet.CurrentPhase);

            // 첫 번째 막대에 [1, 2, 3] 원반이 놓여 있고 나머지 막대는 비어 있음
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, internet.GetPegDiskNumbers(0));
            CollectionAssert.IsEmpty(internet.GetPegDiskNumbers(1));
            CollectionAssert.IsEmpty(internet.GetPegDiskNumbers(2));

            // WindowFront는 크기와 색깔이 전부 동일
            for (int d = 0; d < InternetWindow.HanoiDiskCount; d++)
            {
                Assert.AreEqual(Window.PrimarySortingLayerName, internet.GetDiskFrontSortingLayer(d));
                Assert.AreEqual(Window.SecondarySortingLayerName, internet.GetDiskBackSortingLayer(d));
            }

            Assert.AreEqual(internet.GetDiskFrontSize(0).x, internet.GetDiskFrontSize(1).x, 0.0001f);
            Assert.AreEqual(internet.GetDiskFrontSize(1).x, internet.GetDiskFrontSize(2).x, 0.0001f);
            Assert.AreEqual(internet.GetDiskFrontColor(0), internet.GetDiskFrontColor(1));
            Assert.AreEqual(internet.GetDiskFrontColor(1), internet.GetDiskFrontColor(2));

            // WindowBack(WINDOW.exe 투시)은 크기와 색깔이 서로 다름 (1 > 2 > 3)
            Assert.Greater(internet.GetDiskBackSize(0).x, internet.GetDiskBackSize(1).x);
            Assert.Greater(internet.GetDiskBackSize(1).x, internet.GetDiskBackSize(2).x);
            Assert.AreNotEqual(internet.GetDiskBackColor(0), internet.GetDiskBackColor(1));
            Assert.AreNotEqual(internet.GetDiskBackColor(1), internet.GetDiskBackColor(2));
        }

        [Test]
        public void ClickingPegLiftsTopDiskAndTeleportsWithShakeAndSound()
        {
            float initialTopY = internet.GetDiskPosition(2).y;
            int initialSoundCount = internet.SoundPlayCount;

            // 첫 번째 막대 클릭 -> 맨 위 원반(3번, index 2)이 위로 살짝 뜸
            Assert.IsTrue(internet.TryClickPeg(0));
            Assert.AreEqual(0, internet.SelectedPeg);
            Assert.Greater(internet.GetDiskPosition(2).y, initialTopY + 0.05f);
            Assert.AreEqual(initialSoundCount + 1, internet.SoundPlayCount);

            // 두 번째 막대 클릭 -> 순간이동 + 떨림 이펙트 + 사운드 재생
            Assert.IsTrue(internet.TryClickPeg(1));
            Assert.AreEqual(-1, internet.SelectedPeg);
            CollectionAssert.AreEqual(new[] { 1, 2 }, internet.GetPegDiskNumbers(0));
            CollectionAssert.AreEqual(new[] { 3 }, internet.GetPegDiskNumbers(1));
            Assert.IsTrue(internet.IsDiskShaking(2));
            Assert.AreEqual(initialSoundCount + 2, internet.SoundPlayCount);
            Assert.IsNotNull(internet.LastPlayedClip);
        }

        [Test]
        public void EnforcesHanoiRuleOnlySmallerNumberAllowedBelow()
        {
            // 3번 원반을 두 번째 막대(peg 1)로 이동
            Assert.IsTrue(internet.TryClickPeg(0));
            Assert.IsTrue(internet.TryClickPeg(1));

            // 이제 peg 0 맨 위는 2번 원반, peg 1 맨 위는 3번 원반.
            // 2번 원반의 아래에는 2보다 작은 숫자(1번)만 올 수 있으므로 3번 원반 위에 올릴 수 없음!
            Assert.IsFalse(internet.CanMoveDisk(0, 1));
            Assert.IsTrue(internet.TryClickPeg(0));
            Assert.IsFalse(internet.TryClickPeg(1));

            // 이동이 거부되고 제자리에 남으며 떨림 이펙트가 발생함
            CollectionAssert.AreEqual(new[] { 1, 2 }, internet.GetPegDiskNumbers(0));
            CollectionAssert.AreEqual(new[] { 3 }, internet.GetPegDiskNumbers(1));
            Assert.IsTrue(internet.IsDiskShaking(1));

            // 반대로 2번 원반을 빈 세 번째 막대(peg 2)로 옮긴 뒤, 그 위에 3번 원반을 올리는 것은 가능 (2 < 3)
            Assert.IsTrue(internet.TryClickPeg(0));
            Assert.IsTrue(internet.TryClickPeg(2));
            Assert.IsTrue(internet.CanMoveDisk(1, 2));
            Assert.IsTrue(internet.TryClickPeg(1));
            Assert.IsTrue(internet.TryClickPeg(2));

            CollectionAssert.AreEqual(new[] { 2, 3 }, internet.GetPegDiskNumbers(2));
        }

        [Test]
        public void OverlappingWindowExeBlocksHanoiManipulation()
        {
            internet.MoveTo(Vector2.zero);
            var windowExe = manager.Open("window");
            Assert.IsNotNull(windowExe);

            // WINDOW.exe 창이 인터넷 창과 겹쳐 있을 때는 조작 불가
            windowExe.MoveTo(Vector2.zero);
            Assert.IsTrue(internet.IsHanoiBlockedByWindowExe());
            Assert.IsFalse(internet.TryClickPeg(0));
            Assert.AreEqual(-1, internet.SelectedPeg);

            // WINDOW.exe 창을 멀리 치우면 조작 가능
            windowExe.MoveTo(new Vector2(25f, 25f));
            Assert.IsFalse(internet.IsHanoiBlockedByWindowExe());
            Assert.IsTrue(internet.TryClickPeg(0));
            Assert.AreEqual(0, internet.SelectedPeg);
        }

        [Test]
        public void SolvingHanoiConnectsNetworkAndTransitionsToPhase2()
        {
            // 3개 원반을 첫 번째 막대(0)에서 세 번째 막대(2)로 옮기는 7단계 해법
            int[,] moves =
            {
                { 0, 2 },
                { 0, 1 },
                { 2, 1 },
                { 0, 2 },
                { 1, 0 },
                { 1, 2 },
                { 0, 2 }
            };

            for (int i = 0; i < moves.GetLength(0); i++)
            {
                int from = moves[i, 0];
                int to = moves[i, 1];
                Assert.IsTrue(internet.TryClickPeg(from), $"Step {i} select failed");
                Assert.IsTrue(internet.TryClickPeg(to), $"Step {i} move failed");
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, internet.GetPegDiskNumbers(2));
            Assert.IsTrue(wifi.IsConnected);
            Assert.IsTrue(internet.IsOnline);
            Assert.AreEqual(InternetWindow.InternetPhase.SearchHistory, internet.CurrentPhase);
        }

        [Test]
        public void SettingOfflineIconSpriteUpdatesRenderers()
        {
            var tex = new Texture2D(2, 2);
            var testSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                internet.SetOfflineIconSprite(testSprite);
                Assert.AreEqual(testSprite, internet.OfflineIconSprite);

                var frontRenderer = internet.transform.Find("Primary/IE_OfflineIconFront")?.GetComponent<SpriteRenderer>();
                var backRenderer = internet.transform.Find("Secondary/IE_BackContent/IE_OfflineIconBack")?.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(frontRenderer);
                Assert.IsNotNull(backRenderer);
                Assert.AreEqual(testSprite, frontRenderer.sprite);
                Assert.AreEqual(testSprite, backRenderer.sprite);
                Assert.IsTrue(frontRenderer.enabled);
                Assert.IsTrue(backRenderer.enabled);
            }
            finally
            {
                Object.DestroyImmediate(testSprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Phase2RendersNonTextOnWindowBackAndDecodesWhenCoveredByWindowExe()
        {
            wifi.SetConnected(true);
            internet.MoveTo(Vector2.zero);
            internet.SetDropdownOpen(true);

            Assert.IsTrue(internet.IsOnline);
            Assert.IsTrue(internet.DropdownOpen);
            Assert.IsFalse(internet.IsDecoded);

            // 비-텍스트 요소(브라우저 배경, 검색창, 목록 배경)는 WindowBack에서 렌더링
            var backArtwork = internet.transform.Find("Secondary/IE_BackContent/IE_BackBrowserArtwork")?.GetComponent<SpriteRenderer>();
            var listBorder = internet.transform.Find("Secondary/IE_BackContent/IE_ListBorder")?.GetComponent<SpriteRenderer>();
            var frontMask = internet.transform.Find("Primary/IE_FrontThirdRowMask")?.GetComponent<SpriteRenderer>();

            Assert.IsNotNull(backArtwork);
            Assert.IsNotNull(listBorder);
            Assert.IsNotNull(frontMask);
            Assert.IsTrue(backArtwork.gameObject.activeInHierarchy);
            Assert.AreEqual(Window.SecondarySortingLayerName, backArtwork.sortingLayerName);
            Assert.AreEqual(Window.SecondarySortingLayerName, listBorder.sortingLayerName);
            Assert.IsTrue(frontMask.gameObject.activeInHierarchy);
            Assert.AreEqual(Window.PrimarySortingLayerName, frontMask.sortingLayerName);

            // 텍스트는 WindowFront와 WindowBack 모두에서 렌더링되며, 해독 전 3번째 행은 Front(글리치)와 Back("◷  총")이 다름
            Assert.AreEqual(Window.PrimarySortingLayerName, internet.GetFrontRowSortingLayer(2));
            Assert.AreEqual(Window.SecondarySortingLayerName, internet.GetBackRowSortingLayer(2));
            Assert.AreEqual("◷  총", internet.GetBackRowText(2));
            Assert.AreNotEqual("◷  총", internet.GetFrontRowText(2));

            var windowExe = manager.Open("window");
            Assert.IsNotNull(windowExe);

            // 부분 겹침만으로는 해독되지 않음
            Rect row2Rect = internet.GetSearchRowWorldRect(2);
            windowExe.MoveTo(new Vector2(row2Rect.xMax + windowExe.Size.x * 0.4f, row2Rect.center.y));
            Assert.IsFalse(internet.TryRevealThirdHistory());
            Assert.IsFalse(internet.IsDecoded);
            Assert.AreNotEqual("◷  총", internet.GetFrontRowText(2));

            // WINDOW.exe로 3번째 검색 결과를 완전히 가리면 해독되어 WindowFront 내용도 WindowBack과 동일하게 바뀜
            windowExe.MoveTo(row2Rect.center);
            Assert.IsTrue(internet.TryRevealThirdHistory());
            Assert.IsTrue(internet.IsDecoded);
            Assert.AreEqual(internet.GetBackRowText(2), internet.GetFrontRowText(2));
            Assert.AreEqual("◷  총", internet.GetFrontRowText(2));
            Assert.IsFalse(frontMask.gameObject.activeSelf);
        }

        [Test]
        public void Phase3TransitionsTo404AndGunResultsWithoutOpeningPopups()
        {
            var tex404 = new Texture2D(2, 2);
            var sprite404 = Sprite.Create(tex404, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            var texGun = new Texture2D(2, 2);
            var spriteGun = Sprite.Create(texGun, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

            try
            {
                wifi.SetConnected(true);
                internet.SetDropdownOpen(true);
                internet.SetNotFound404Sprite(sprite404);
                internet.SetGunResultSprite(spriteGun);

                var front404 = internet.transform.Find("Primary/IE_NotFound404Front")?.GetComponent<SpriteRenderer>();
                var back404 = internet.transform.Find("Secondary/IE_BackContent/IE_NotFound404Back")?.GetComponent<SpriteRenderer>();
                var frontGun = internet.transform.Find("Primary/IE_GunResultFront")?.GetComponent<SpriteRenderer>();
                var backGun = internet.transform.Find("Secondary/IE_BackContent/IE_GunResultBack")?.GetComponent<SpriteRenderer>();
                var searchBorder = internet.transform.Find("Secondary/IE_BackContent/IE_SearchBorder")?.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(front404);
                Assert.IsNotNull(back404);
                Assert.IsNotNull(frontGun);
                Assert.IsNotNull(backGun);
                Assert.IsNotNull(searchBorder);

                int processCountBefore = manager.ActiveProcesses.Count;

                // 1. 총을 제외한 검색 기록(index 0) 클릭 -> Phase 3 404 결과로 전환
                internet.ChooseHistory(0);
                Assert.AreEqual(InternetWindow.InternetPhase.Result404, internet.CurrentPhase);
                Assert.AreEqual(processCountBefore, manager.ActiveProcesses.Count, "팝업 창을 열지 않고 내부에서만 띄워야 함");

                // 검색창 숨김, 404 이미지가 WindowFront & WindowBack 양쪽에 활성화
                Assert.IsFalse(searchBorder.gameObject.activeSelf);
                Assert.IsTrue(front404.gameObject.activeSelf);
                Assert.IsTrue(back404.gameObject.activeSelf);
                Assert.IsTrue(front404.enabled);
                Assert.IsTrue(back404.enabled);
                Assert.IsFalse(frontGun.gameObject.activeSelf);
                Assert.IsFalse(backGun.gameObject.activeSelf);

                // 2. 총 검색 기록은 해독 전에는 클릭해도 반응 없음
                Assert.IsFalse(internet.IsDecoded);
                internet.ChooseHistory(2);
                Assert.AreEqual(InternetWindow.InternetPhase.Result404, internet.CurrentPhase);

                // WINDOW.exe로 해독 완료
                var windowExe = manager.Open("window");
                Rect row2Rect = internet.GetSearchRowWorldRect(2);
                windowExe.MoveTo(row2Rect.center);
                internet.SetDropdownOpen(true);
                Assert.IsTrue(internet.TryRevealThirdHistory());
                Assert.IsTrue(internet.IsDecoded);

                // 3. 해독 후 총 검색 기록(index 2) 클릭 -> Phase 3 총 결과로 전환
                int processCountBeforeGun = manager.ActiveProcesses.Count;
                internet.ChooseHistory(2);
                Assert.AreEqual(InternetWindow.InternetPhase.ResultGun, internet.CurrentPhase);
                Assert.AreEqual(processCountBeforeGun, manager.ActiveProcesses.Count, "gun-file 팝업 창을 열지 않고 내부에서만 띄워야 함");

                // 404 이미지 숨김, 총 이미지가 WindowFront & WindowBack 양쪽에 활성화
                Assert.IsFalse(searchBorder.gameObject.activeSelf);
                Assert.IsFalse(front404.gameObject.activeSelf);
                Assert.IsFalse(back404.gameObject.activeSelf);
                Assert.IsTrue(frontGun.gameObject.activeSelf);
                Assert.IsTrue(backGun.gameObject.activeSelf);
                Assert.IsTrue(frontGun.enabled);
                Assert.IsTrue(backGun.enabled);
            }
            finally
            {
                Object.DestroyImmediate(sprite404);
                Object.DestroyImmediate(tex404);
                Object.DestroyImmediate(spriteGun);
                Object.DestroyImmediate(texGun);
            }
        }
    }
}

