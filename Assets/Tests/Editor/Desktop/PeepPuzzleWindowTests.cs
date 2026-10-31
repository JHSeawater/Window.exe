using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class PeepPuzzleWindowTests
    {
        private GameObject windowGo;
        private PeepPuzzleWindow puzzleWindow;
        private List<PeepPuzzleObject> clues;
        private PeepPuzzleObject danger;
        private PeepPuzzleObject watcher;
        private PeepPuzzleObject escape;

        [SetUp]
        public void SetUp()
        {
            windowGo = new GameObject("PeepPuzzleWindow");
            puzzleWindow = windowGo.AddComponent<PeepPuzzleWindow>();
            puzzleWindow.InitializeVisuals();

            // Secondary 하위 오브젝트 계층 생성
            var secondary = windowGo.transform.Find("Secondary");
            if (secondary == null)
            {
                secondary = new GameObject("Secondary").transform;
                secondary.SetParent(windowGo.transform, false);
            }

            clues = new List<PeepPuzzleObject>();
            for (var i = 0; i < 4; i++)
            {
                var clueGo = new GameObject($"Clue_{i}");
                clueGo.transform.SetParent(secondary, false);
                clueGo.transform.localPosition = new Vector3(-1f + i * 0.5f, 0f, 0f);
                var clueObj = clueGo.AddComponent<PeepPuzzleObject>();
                clueObj.Setup(PeepObjectType.Clue, new Vector2(0.4f, 0.4f));
                clues.Add(clueObj);
            }

            var dangerGo = new GameObject("Danger");
            dangerGo.transform.SetParent(secondary, false);
            dangerGo.transform.localPosition = new Vector3(1f, 1f, 0f);
            danger = dangerGo.AddComponent<PeepPuzzleObject>();
            danger.Setup(PeepObjectType.Danger, new Vector2(0.5f, 0.5f));

            var watcherGo = new GameObject("Watcher");
            watcherGo.transform.SetParent(secondary, false);
            watcherGo.transform.localPosition = new Vector3(-1f, -1f, 0f);
            watcher = watcherGo.AddComponent<PeepPuzzleObject>();
            watcher.Setup(PeepObjectType.Watcher, new Vector2(0.5f, 0.5f));

            var escapeGo = new GameObject("Escape");
            escapeGo.transform.SetParent(secondary, false);
            escapeGo.transform.localPosition = new Vector3(1f, -1f, 0f);
            escape = escapeGo.AddComponent<PeepPuzzleObject>();
            escape.Setup(PeepObjectType.Escape, new Vector2(0.6f, 0.6f));

            puzzleWindow.SetupPuzzleObjects(clues, danger, watcher, escape);
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
        public void WindowCannotBeResizedOrMaximized()
        {
            var initialSize = puzzleWindow.Size;

            Assert.IsNull(puzzleWindow.ResizeAt(Vector2.zero));
            Assert.IsFalse(puzzleWindow.IsPointerOverResizeHandle(Vector2.zero));

            puzzleWindow.Resize(new Vector2(10f, 10f));
            Assert.AreEqual(initialSize, puzzleWindow.Size);

            puzzleWindow.Maximize();
            Assert.IsFalse(puzzleWindow.IsMaximized);
            Assert.AreEqual(initialSize, puzzleWindow.Size);
        }

        [Test]
        public void SecondaryChildrenMaintainWindowBackSortingLayerAfterApplySorting()
        {
            puzzleWindow.ApplySorting(64);

            var secondary = windowGo.transform.Find("Secondary");
            Assert.IsNotNull(secondary);

            var renderers = secondary.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0);

            foreach (var r in renderers)
            {
                Assert.AreEqual(Window.SecondarySortingLayerName, r.sortingLayerName,
                    $"Renderer on {r.gameObject.name} should be {Window.SecondarySortingLayerName} but was {r.sortingLayerName}");
            }
        }

        [Test]
        public void CluesMustAllBeCollectedBeforeEscapeCanBeClicked()
        {
            Assert.IsFalse(escape.IsUnlocked);

            // 단서 3개만 수집
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(clues[i].Click());
                Assert.IsTrue(clues[i].IsCollected);
            }

            Assert.IsFalse(puzzleWindow.AreAllCluesCollected);
            Assert.IsFalse(escape.IsUnlocked);

            // 탈출 오브젝트 클릭 시도 -> 잠겨있으므로 클리어 실패
            var cleared = false;
            puzzleWindow.OnCleared += () => cleared = true;
            Assert.IsFalse(escape.Click());
            Assert.IsFalse(cleared);
            Assert.IsFalse(puzzleWindow.IsCleared);

            // 마지막 4번째 단서 수집
            Assert.IsTrue(clues[3].Click());
            Assert.IsTrue(clues[3].IsCollected);

            Assert.IsTrue(puzzleWindow.AreAllCluesCollected);
            puzzleWindow.SetupPuzzleObjects(clues, danger, watcher, escape); // 갱신
            Assert.IsTrue(escape.IsUnlocked);

            // 이제 탈출 오브젝트 클릭 가능
            Assert.IsTrue(escape.Click());
        }

        [Test]
        public void EscapeClickTriggersClearedAndClose()
        {
            var clearedCalled = false;
            var closeRequested = false;

            puzzleWindow.OnCleared += () => clearedCalled = true;
            puzzleWindow.OnCloseRequested += _ => closeRequested = true;

            // 단서 4개 모두 수집
            foreach (var c in clues) c.Click();
            puzzleWindow.SetupPuzzleObjects(clues, danger, watcher, escape);

            // 탈출 오브젝트 클릭
            var success = puzzleWindow.HandlePeepClick(escape.transform.position);

            Assert.IsTrue(success);
            Assert.IsTrue(clearedCalled);
            Assert.IsTrue(puzzleWindow.IsCleared);
            Assert.IsTrue(closeRequested);
        }

        [Test]
        public void DangerObjectTriggersGameOverAfter3SecondsInViewAndResetsWhenOutOfView()
        {
            var gameOverTriggered = false;
            string gameOverReason = null;
            puzzleWindow.OnGameOverTriggered += reason =>
            {
                gameOverTriggered = true;
                gameOverReason = reason;
            };

            // 1. 위험 오브젝트가 윈도우 시야 내에 위치
            var dangerPos = (Vector2)danger.transform.position;
            puzzleWindow.OverridePeepViewport = new Rect(dangerPos.x - 1f, dangerPos.y - 1f, 2f, 2f);

            // 2.9초 틱 -> 아직 게임오버 안 됨
            puzzleWindow.ManualTick(2.9f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(2.9f, puzzleWindow.DangerTimer, 0.001f);

            // 시야에서 벗어남 -> 즉시 0초로 리셋!
            puzzleWindow.OverridePeepViewport = new Rect(-100f, -100f, 1f, 1f);
            puzzleWindow.ManualTick(0.1f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(0f, puzzleWindow.DangerTimer, 0.001f);

            // 다시 시야에 들어옴
            puzzleWindow.OverridePeepViewport = new Rect(dangerPos.x - 1f, dangerPos.y - 1f, 2f, 2f);
            puzzleWindow.ManualTick(1.5f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(1.5f, puzzleWindow.DangerTimer, 0.001f);

            // 추가 1.6초 경과 (총 3.1초) -> 게임오버 발생!
            puzzleWindow.ManualTick(1.6f);
            Assert.IsTrue(gameOverTriggered);
            Assert.IsTrue(puzzleWindow.IsGameOver);
            StringAssert.Contains("위험 대상", gameOverReason);
        }

        [Test]
        public void WatcherObjectTriggersGameOverAfter10SecondsOutOfViewAndResetsWhenInView()
        {
            var gameOverTriggered = false;
            string gameOverReason = null;
            puzzleWindow.OnGameOverTriggered += reason =>
            {
                gameOverTriggered = true;
                gameOverReason = reason;
            };

            var watcherPos = (Vector2)watcher.transform.position;

            // 1. 처음엔 윈도우 시야 내에 있음 -> 타이머 0 유지
            puzzleWindow.OverridePeepViewport = new Rect(watcherPos.x - 1f, watcherPos.y - 1f, 2f, 2f);
            puzzleWindow.ManualTick(3.0f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(0f, puzzleWindow.WatcherTimer, 0.001f);

            // 2. 윈도우 시야 밖으로 벗어남 -> 미관측 타이머 누적 시작
            puzzleWindow.OverridePeepViewport = new Rect(100f, 100f, 1f, 1f);
            puzzleWindow.ManualTick(9.9f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(9.9f, puzzleWindow.WatcherTimer, 0.001f);

            // 3. 윈도우를 다시 비춤 -> 타이머 0으로 리셋!
            puzzleWindow.OverridePeepViewport = new Rect(watcherPos.x - 1f, watcherPos.y - 1f, 2f, 2f);
            puzzleWindow.ManualTick(0.1f);
            Assert.IsFalse(gameOverTriggered);
            Assert.AreEqual(0f, puzzleWindow.WatcherTimer, 0.001f);

            // 4. 다시 시야 밖으로 벗어나서 10.1초 경과 -> 게임오버 발생!
            puzzleWindow.OverridePeepViewport = new Rect(100f, 100f, 1f, 1f);
            puzzleWindow.ManualTick(10.1f);
            Assert.IsTrue(gameOverTriggered);
            Assert.IsTrue(puzzleWindow.IsGameOver);
            StringAssert.Contains("감시 대상", gameOverReason);
        }

        [Test]
        public void CannotClickCluesOutsidePeepViewport()
        {
            // 윈도우 뷰포트를 다른 곳에 설정
            puzzleWindow.OverridePeepViewport = new Rect(100f, 100f, 2f, 2f);

            // 단서 위치 클릭 시도 -> 뷰포트 밖이므로 클릭 실패
            var clicked = puzzleWindow.HandlePeepClick(clues[0].transform.position);
            Assert.IsFalse(clicked);
            Assert.IsFalse(clues[0].IsCollected);
        }

        [Test]
        public void MultipleDangerObjects_AnyOneReaching3SecondsTriggersGameOver()
        {
            var secondary = windowGo.transform.Find("Secondary");
            var dangerBGo = new GameObject("DangerB");
            dangerBGo.transform.SetParent(secondary, false);
            dangerBGo.transform.localPosition = new Vector3(3f, 3f, 0f);
            var dangerB = dangerBGo.AddComponent<PeepPuzzleObject>();
            dangerB.Setup(PeepObjectType.Danger, new Vector2(0.5f, 0.5f));

            puzzleWindow.SetupPuzzleObjects(clues, new[] { danger, dangerB }, new[] { watcher }, escape);

            var gameOverTriggered = false;
            puzzleWindow.OnGameOverTriggered += _ => gameOverTriggered = true;

            // Danger A만 시야에 두고 2.5초 경과
            var dangerAPos = (Vector2)danger.transform.position;
            puzzleWindow.OverridePeepViewport = new Rect(dangerAPos.x - 0.5f, dangerAPos.y - 0.5f, 1f, 1f);
            puzzleWindow.ManualTick(2.5f);
            Assert.IsFalse(gameOverTriggered);

            // Danger B 쪽으로 시야 이동 (Danger A는 시야 이탈 -> 0초 리셋)
            var dangerBPos = (Vector2)dangerB.transform.position;
            puzzleWindow.OverridePeepViewport = new Rect(dangerBPos.x - 0.5f, dangerBPos.y - 0.5f, 1f, 1f);
            puzzleWindow.ManualTick(0.1f);
            Assert.IsFalse(gameOverTriggered);

            // Danger B를 3초 이상 응시 -> 게임오버 발생!
            puzzleWindow.ManualTick(3.0f);
            Assert.IsTrue(gameOverTriggered);
            Assert.IsTrue(puzzleWindow.IsGameOver);
        }

        [Test]
        public void MultipleWatcherObjects_AnyOneLeftUnseenFor10SecondsTriggersGameOver()
        {
            var secondary = windowGo.transform.Find("Secondary");
            var watcherBGo = new GameObject("WatcherB");
            watcherBGo.transform.SetParent(secondary, false);
            watcherBGo.transform.localPosition = new Vector3(-3f, -3f, 0f);
            var watcherB = watcherBGo.AddComponent<PeepPuzzleObject>();
            watcherB.Setup(PeepObjectType.Watcher, new Vector2(0.5f, 0.5f));

            puzzleWindow.SetupPuzzleObjects(clues, new[] { danger }, new[] { watcher, watcherB }, escape);

            var gameOverTriggered = false;
            puzzleWindow.OnGameOverTriggered += _ => gameOverTriggered = true;

            // Watcher A만 비추고 있고 Watcher B는 방치되는 상황
            var watcherAPos = (Vector2)watcher.transform.position;
            puzzleWindow.OverridePeepViewport = new Rect(watcherAPos.x - 0.5f, watcherAPos.y - 0.5f, 1f, 1f);

            // 9초 동안 A만 봄
            puzzleWindow.ManualTick(9.0f);
            Assert.IsFalse(gameOverTriggered);

            // 1.5초 더 A만 봄 (Watcher B는 총 10.5초 방치됨) -> 게임오버!
            puzzleWindow.ManualTick(1.5f);
            Assert.IsTrue(gameOverTriggered);
            Assert.IsTrue(puzzleWindow.IsGameOver);
        }
    }
}

