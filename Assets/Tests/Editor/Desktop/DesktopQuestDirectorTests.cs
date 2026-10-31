using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WindowExe.Desktop.Desktop.Window;
using W = WindowExe.Desktop.Desktop.Window.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture]
    public class DesktopQuestDirectorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private DesktopController desktop;
        private WindowManager windowManager;
        private WifiConnection wifiConnection;
        private DesktopQuestDirector director;

        private DesktopIconView recycleIcon;
        private DesktopIconView memoIcon;
        private DesktopIconView internetIcon;
        private DesktopIconView windowExeIcon;
        private DesktopIconView imageIcon;
        private DesktopIconView keyFileIcon;

        [SetUp]
        public void SetUp()
        {
            WifiConnection.ResetInstance(null);

            var canvasGo = Track(new GameObject("TestDesktopCanvas", typeof(RectTransform), typeof(Canvas)));
            desktop = canvasGo.AddComponent<DesktopController>();

            var iconAreaGo = Track(new GameObject("IconArea", typeof(RectTransform)));
            var iconArea = iconAreaGo.GetComponent<RectTransform>();
            iconArea.SetParent(canvasGo.transform, false);
            iconArea.sizeDelta = new Vector2(1920f, 1000f);

            var windowLayerGo = Track(new GameObject("WindowLayer", typeof(RectTransform)));
            var windowLayer = windowLayerGo.GetComponent<RectTransform>();
            windowLayer.SetParent(canvasGo.transform, false);

            desktop.Configure(iconArea, windowLayer);

            recycleIcon = CreateIcon(iconArea, "Icon_recycle-bin", "recycle-bin", "휴지통", new Vector2(8f, -8f));
            memoIcon = CreateIcon(iconArea, "Icon_memo", "memo", "메모.txt", new Vector2(8f, -172f));
            internetIcon = CreateIcon(iconArea, "Icon_internet", "internet", "인터넷", new Vector2(8f, -336f));
            imageIcon = CreateIcon(iconArea, "Icon-peep", "peepWindow", "image.png", new Vector2(8f, -500f));
            keyFileIcon = CreateIcon(iconArea, "KeyFileIcon", "key-file", "key.txt", new Vector2(8f, -664f));
            keyFileIcon.gameObject.AddComponent<KeyFileMechanic>();
            windowExeIcon = CreateIcon(iconArea, "Icon_window", "window", "WINDOW(1).exe", new Vector2(164f, -8f));

            var managerGo = Track(new GameObject("TestWindowManager"));
            windowManager = managerGo.AddComponent<WindowManager>();

            var memoPrefab = CreateWindowPrefab<NotepadWindow>("MemoPrefab", new Vector2(5.2f, 3.4f));
            var windowExePrefab = CreateWindowPrefab<WindowExeWindow>("WindowExePrefab", new Vector2(4.0f, 3.0f));
            var internetPrefab = CreateWindowPrefab<InternetWindow>("InternetPrefab", new Vector2(6.0f, 4.0f));

            windowManager.Configure(managerGo.transform, new[]
            {
                new WindowPrefabEntry { id = "memo", prefab = memoPrefab },
                new WindowPrefabEntry { id = "window", prefab = windowExePrefab },
                new WindowPrefabEntry { id = "internet", prefab = internetPrefab }
            });

            var wifiGo = Track(new GameObject("TestWifi"));
            wifiConnection = wifiGo.AddComponent<WifiConnection>();
            InvokePrivate(wifiConnection, "Awake");
            wifiConnection.SetConnected(false);

            director = managerGo.AddComponent<DesktopQuestDirector>();
            director.Configure(desktop, windowManager, wifiConnection, imageIcon, keyFileIcon);
            director.ApplyInitialIconVisibility();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
            WifiConnection.ResetInstance(null);
        }

        [Test]
        public void StartsWithFourActiveIconsAndOpensMemoWindowAndAllowsInternetFromStart()
        {
            InvokePrivate(director, "Start");

            // 초기 활성 아이콘 4개 확인 (휴지통, 메모.txt, 인터넷, WINDOW(1).exe)
            Assert.IsTrue(recycleIcon.gameObject.activeSelf);
            Assert.IsTrue(memoIcon.gameObject.activeSelf);
            Assert.IsTrue(internetIcon.gameObject.activeSelf);
            Assert.IsTrue(windowExeIcon.gameObject.activeSelf);

            // image.png 및 key.txt는 초기 비활성(SetActive(false)) 상태
            Assert.IsFalse(imageIcon.gameObject.activeSelf);
            Assert.IsFalse(keyFileIcon.gameObject.activeSelf);

            // 시작 시 memo.txt 창이 자동으로 열려 있어야 함
            Assert.IsTrue(windowManager.IsWindowOpen("memo"));

            // 인터넷 창도 처음부터 열 수 있어야 함 (비밀번호 단서 확인용)
            var internetWin = windowManager.Open("internet");
            Assert.IsNotNull(internetWin);
            Assert.IsTrue(windowManager.IsWindowOpen("internet"));
        }

        [Test]
        public void OverlappingWindowExeWithMemoImmediatelyRevealsImageIconWithoutDelay()
        {
            InvokePrivate(director, "Start");

            var memoWin = windowManager.GetProcess("memo").Instance;
            Assert.IsNotNull(memoWin);
            memoWin.MoveTo(new Vector2(-4f, 0f));

            var windowExe = windowManager.Open("window");
            Assert.IsNotNull(windowExe);

            // 서로 떨어져 있을 때는 HELP ME 미감지 및 image.png 비활성 유지
            windowExe.MoveTo(new Vector2(5f, 0f));
            director.ManualTick(0f);
            Assert.IsFalse(director.IsHelpMeSeen);
            Assert.IsFalse(imageIcon.gameObject.activeSelf);

            // 겹치는 위치로 이동하는 즉시 (0초 대기) image.png 아이콘 활성화
            windowExe.MoveTo(new Vector2(-3.5f, 0f));
            Assert.IsTrue(director.CheckHelpMeOverlap());
            Assert.IsTrue(director.IsHelpMeSeen);
            Assert.IsTrue(imageIcon.gameObject.activeSelf);
        }

        [Test]
        public void PeepPuzzleWindowPrefabHasEscapeDisabledAndDoesNotCloseOnAllClues()
        {
            var prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Desktop/Windows/PeepPuzzleWindow.prefab");
            Assert.IsNotNull(prefabGo);

            var instanceGo = Track(Object.Instantiate(prefabGo));
            var peepWindow = instanceGo.GetComponent<PeepPuzzleWindow>();
            Assert.IsNotNull(peepWindow);
            peepWindow.InitializeVisuals();

            // 출구 오브젝트는 비활성/미할당 상태여야 함
            Assert.IsNull(peepWindow.EscapeObject);
            var escapeChild = instanceGo.transform.Find("Secondary/Escape");
            if (escapeChild != null)
            {
                Assert.IsFalse(escapeChild.gameObject.activeSelf);
            }

            // 단서 4개를 모두 클릭해도 창이 강제로 닫히지 않고 단서 열람 상태를 유지해야 함
            bool closeRequested = false;
            peepWindow.OnCloseRequested += _ => closeRequested = true;

            foreach (var clue in peepWindow.Clues)
            {
                if (clue != null) clue.Click();
            }

            Assert.IsTrue(peepWindow.AreAllCluesCollected);
            Assert.IsFalse(closeRequested);
        }

        [Test]
        public void ConnectingWifiImmediatelyRevealsKeyFileIcon()
        {
            InvokePrivate(director, "Start");

            Assert.IsFalse(director.IsKeyFileRevealed);
            Assert.IsFalse(keyFileIcon.gameObject.activeSelf);

            // 하노이 탑 퍼즐 해결 등으로 Wi-Fi가 연결되면 key.txt 아이콘 활성화
            wifiConnection.SetConnected(true);

            Assert.IsTrue(director.IsKeyFileRevealed);
            Assert.IsTrue(keyFileIcon.gameObject.activeSelf);
        }

        private DesktopIconView CreateIcon(RectTransform parent, string objectName, string itemId, string displayName, Vector2 anchoredPos)
        {
            var go = Track(new GameObject(objectName, typeof(RectTransform), typeof(Image)));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(140f, 148f);
            rect.anchoredPosition = anchoredPos;

            var iconGo = Track(new GameObject("Icon", typeof(RectTransform), typeof(Image)));
            iconGo.transform.SetParent(rect, false);

            var labelGo = Track(new GameObject("Label", typeof(RectTransform), typeof(Text)));
            labelGo.transform.SetParent(rect, false);

            var item = Track(ScriptableObject.CreateInstance<DesktopItemDefinition>());
            item.Configure(itemId, displayName, null);

            var view = go.AddComponent<DesktopIconView>();
            view.Configure(desktop, item, go.GetComponent<Image>(), iconGo.GetComponent<Image>(), labelGo.GetComponent<Text>());
            return view;
        }

        private T CreateWindowPrefab<T>(string name, Vector2 windowSize) where T : W
        {
            var go = Track(new GameObject(name));
            go.SetActive(false);
            var win = go.AddComponent<T>();
            win.InitializeVisuals();
            win.ApplyGeometry(Vector2.zero, windowSize);
            return win;
        }

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, $"Method {methodName} not found on {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}

