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
    }
}

