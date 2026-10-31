using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using WindowExe.Desktop.Desktop.Window;
using W = WindowExe.Desktop.Desktop.Window.Window;

namespace WindowExe.Desktop.Tests
{
    [TestFixture(CRTQuality.Full)]
    [TestFixture(CRTQuality.Off)]
    public sealed partial class DesktopSceneInteractionTests
    {
        private readonly CRTQuality crtQuality;
        public DesktopSceneInteractionTests(CRTQuality quality) { crtQuality = quality; }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private EditorWindow gameView;
        private PropertyInfo sizeIndex;
        private object sizeGroup;
        private int previousSize;
        private readonly List<int> addedSizes = new List<int>();
        private DesktopInputTestScope inputScope;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                Assert.That(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save user scene changes before testing the shared desktop scene.");
            EditorSceneManager.OpenScene("Assets/Scenes/Desktop.unity");
            yield return new EnterPlayMode();
            inputScope = new DesktopInputTestScope();
            Assert.That(CRTOverlay.Instance, Is.Not.Null);
            CRTOverlay.Instance.SetQuality(crtQuality);
            var assembly = typeof(Editor).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            gameView = EditorWindow.GetWindow(viewType);
            sizeIndex = viewType.GetProperty("selectedSizeIndex", Flags);
            previousSize = (int)sizeIndex.GetValue(gameView);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.BaseType.GetProperty("instance", Flags).GetValue(null);
            sizeGroup = sizesType.GetProperty("currentGroup", Flags).GetValue(sizes);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameView != null && sizeIndex != null)
            {
                sizeIndex.SetValue(gameView, previousSize);
                for (var i = addedSizes.Count - 1; i >= 0; i--)
                    sizeGroup.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(sizeGroup, new object[] { addedSizes[i] });
                gameView.Repaint();
            }
            addedSizes.Clear();
            inputScope?.Dispose();
            inputScope = null;
            yield return new ExitPlayMode();
        }

        private void SetResolution(int width, int height)
        {
            // Unity exposes fixed Game View sizes through Editor-only internal types.
            var assembly = typeof(Editor).Assembly;
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, Flags, null,
                new[] { Enum.Parse(modeType, "FixedResolution"), (object)width, height, "Desktop region validation" }, null);
            var groupType = sizeGroup.GetType();
            groupType.GetMethod("AddCustomSize", Flags).Invoke(sizeGroup, new[] { size });
            var index = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(sizeGroup, null) - 1;
            addedSizes.Add(index);
            sizeIndex.SetValue(gameView, index);
            gameView.Repaint();
        }

        [UnityTest]
        public IEnumerator SharedSceneSelectsAndMovesGroupAt720pAnd1080p()
        {
            var desktop = Object.FindAnyObjectByType<DesktopController>();
            var box = desktop.GetComponentInChildren<DesktopSelectionBox>(true);
            Assert.That(box, Is.Not.Null, "The shared scene must contain its serialized selection view.");
            var events = Object.FindAnyObjectByType<EventSystem>();
            var module = events.GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("SharedDesktopValidationMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var positions = new Dictionary<DesktopIconView, Vector3>();
            foreach (var icon in desktop.Icons) positions.Add(icon, icon.Rect.localPosition);
            var opened = 0;
            desktop.OpenRequested += _ => opened++;
            try
            {
                foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    SetResolution(resolution.x, resolution.y);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(resolution));
                    foreach (var entry in positions) entry.Key.Rect.localPosition = entry.Value;
                    desktop.Select(null);
                    var first = desktop.Icons[0];
                    var second = desktop.Icons[1];
                    var bounds = DesktopLayout.Union(DesktopLayout.BoundsInParent(first.Rect, desktop.IconArea),
                        DesktopLayout.BoundsInParent(second.Rect, desktop.IconArea));
                    var start = ScreenPoint(desktop, new Vector2(bounds.xMin - 8, bounds.yMax + 8));
                    var end = ScreenPoint(desktop, new Vector2(bounds.xMax + 8, bounds.yMin - 8));
                    yield return Send(mouse, start, false);
                    yield return Send(mouse, start, true);
                    yield return Send(mouse, end, true);
                    Assert.That(desktop.IsSelecting, Is.True, Diagnostic());
                    Assert.That(box.Visible, Is.True);
                    Assert.That(desktop.SelectedIcons, Is.EquivalentTo(new[] { first, second }));
                    var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/DesktopValidation",
                        $"region-{resolution.x}x{resolution.y}.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    yield return Send(mouse, end, false);
                    Assert.That(box.Visible, Is.False);
                    Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                    var firstStart = first.Rect.localPosition;
                    var secondStart = second.Rect.localPosition;
                    var press = Center(first.Rect);
                    Vector2 movement = new Vector2(500, -30);
                    var target = press + ScreenPoint(desktop, movement) - ScreenPoint(desktop, Vector2.zero);
                    yield return Send(mouse, press, false);
                    yield return Send(mouse, press, true);
                    yield return Send(mouse, target, true);
                    yield return Send(mouse, target, false);
                    Assert.That(Vector2.Distance(first.Rect.localPosition - firstStart, movement), Is.LessThan(0.1));
                    Assert.That(Vector2.Distance(second.Rect.localPosition - secondStart, movement), Is.LessThan(0.1));
                    Assert.That(opened, Is.Zero);
                    Assert.That(File.Exists(path), Is.True, "Selection screenshot was not written.");
                }
            }
            finally
            {
                desktop.CancelInteraction();
                foreach (var entry in positions)
                    if (entry.Key != null) entry.Key.Rect.localPosition = entry.Value;
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator TaskbarClicksOverflowAndReenableWorkAtBothResolutions()
        {
            var desktop = Object.FindAnyObjectByType<DesktopController>();
            var bar = Object.FindAnyObjectByType<TaskbarController>();
            var source = Object.FindAnyObjectByType<WindowTaskbarSource>();
            var area = desktop.GetComponent<DesktopWorkArea>();
            Assert.That(bar, Is.Not.Null);
            var module = Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("TaskbarSceneMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            try
            {
                foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                {
                    SetResolution(resolution.x, resolution.y);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    WindowManager.Instance.ResetWindows();
                    var icon = desktop.Icons[0];
                    var iconPoint = Center(icon.Rect);
                    yield return Send(mouse, iconPoint, false);
                    yield return Send(mouse, iconPoint, true);
                    yield return Send(mouse, iconPoint, false);
                    yield return Send(mouse, iconPoint, true);
                    yield return Send(mouse, iconPoint, false);
                    Assert.That(source.GetSnapshot().Windows.Count, Is.EqualTo(1), "Double-click must reach the real adapter once. " + Diagnostic());
                    var id = source.GetSnapshot().Windows[0].WindowId;
                    Assert.That(bar.Buttons.Count, Is.EqualTo(1));
                    var point = Center((RectTransform)bar.Buttons[id].transform);
                    var notifications = 0;
                    Action changed = () => notifications++;
                    source.Changed += changed;
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    source.Changed -= changed;
                    Assert.That(notifications, Is.EqualTo(1), "A button click must dispatch only once.");
                    Assert.That(WindowManager.Instance.FindInstance(id).State == WindowState.Minimized, Is.True);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    Assert.That(WindowManager.Instance.FindInstance(id).State == WindowState.Minimized, Is.False);
                    WindowManager.Instance.ToggleMaximize(WindowManager.Instance.FindInstance(id).Id);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    Assert.That(WindowManager.Instance.FindInstance(id).Instance.IsMaximized, Is.True);
                    Assert.That(WindowManager.Instance.FindInstance(id).State == WindowState.Minimized, Is.False);
                    bar.enabled = false;
                    Assert.That(bar.Buttons, Is.Empty);
                    WindowManager.Instance.Open("photos");
                    var second = WindowManager.Instance.GetProcess("photos").InstanceId;
                    bar.enabled = true;
                    yield return null;
                    yield return null;
                    Assert.That(bar.Buttons.Count, Is.EqualTo(2));
                    WindowManager.Instance.Close("photos");
                    Assert.That(bar.Buttons.ContainsKey(second), Is.False);
                    for (var i = 0; i < 18; i++) {
                        WindowManager.Instance.RegisterPrefab("extra"+i, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Desktop/Windows/memo.prefab").GetComponent<W>());
                        WindowManager.Instance.Open("extra"+i).SetWindowTitle($"기록 {i} - 아주 긴 창 제목이 영역을 벗어나지 않아야 합니다");
                    }
                    yield return null;
                    yield return null;
                    Assert.That(bar.Overflow, Is.True);
                    var before = bar.Content.anchoredPosition.x;
                    var arrow = (RectTransform)bar.transform.Find("ScrollLeft");
                    yield return Send(mouse, Center(arrow), true);
                    yield return Send(mouse, Center(arrow), false);
                    Assert.That(bar.Content.anchoredPosition.x, Is.GreaterThan(before));
                    var active = source.GetSnapshot().ActiveWindowId;
                    var dragStart = Center(bar.Viewport);
                    yield return Send(mouse, dragStart, false);
                    yield return Send(mouse, dragStart, true);
                    yield return Send(mouse, dragStart + new Vector2(80, 0), true);
                    yield return Send(mouse, dragStart + new Vector2(80, 0), false);
                    Assert.That(source.GetSnapshot().ActiveWindowId, Is.EqualTo(active));
                    Assert.That(WindowManager.Instance.FindInstance(active).State == WindowState.Minimized, Is.False);
                    Assert.That(desktop.IsSelecting, Is.False);
                    Assert.That(area.Area.yMin, Is.EqualTo(((RectTransform)desktop.transform).rect.yMin + 48).Within(0.1));
                    Assert.That(desktop.IconArea.offsetMin.y, Is.EqualTo(68).Within(0.1));
                    var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/DesktopValidation/Taskbar",
                        $"taskbar-{resolution.x}x{resolution.y}.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    ScreenCapture.CaptureScreenshot(path);
                    for (var frame = 0; frame < 5; frame++) yield return null;
                    Assert.That(File.Exists(path), Is.True);
                }
            }
            finally
            {
                bar.enabled = true;
                WindowManager.Instance.ResetWindows();
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator TaskbarInputDoesNotLeakAndDesktopDragsCanEndOverBar()
        {
            var desktop = Object.FindAnyObjectByType<DesktopController>();
            var bar = Object.FindAnyObjectByType<TaskbarController>();
            var source = Object.FindAnyObjectByType<WindowTaskbarSource>();
            var work = desktop.GetComponent<DesktopWorkArea>();
            var module = Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("TaskbarDirectionMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var icon = desktop.Icons[0];
            var original = icon.Rect.localPosition;
            try
            {
                WindowManager.Instance.ResetWindows();
                desktop.Select(icon);
                var barPoint = Center((RectTransform)bar.transform);
                var desktopPoint = ScreenPoint(desktop, new Vector2(desktop.IconArea.rect.xMax - 100, -100));
                yield return Send(mouse, barPoint, false);
                yield return Send(mouse, barPoint, true);
                yield return Send(mouse, desktopPoint, true);
                Assert.That(desktop.IsSelecting, Is.False);
                yield return Send(mouse, desktopPoint, false);
                Assert.That(desktop.Selected, Is.EqualTo(icon));
                Assert.That(icon.Rect.localPosition, Is.EqualTo(original));
                yield return Send(mouse, desktopPoint, true);
                yield return Send(mouse, barPoint, true);
                Assert.That(desktop.IsSelecting, Is.True, Diagnostic());
                Assert.That(desktop.SelectionBounds.yMin, Is.GreaterThanOrEqualTo(desktop.IconArea.rect.yMin - 0.1f));
                yield return Send(mouse, barPoint, false);
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(source.GetSnapshot().Windows, Is.Empty);
                desktop.Select(null);
                var press = Center(icon.Rect);
                yield return Send(mouse, press, false);
                yield return Send(mouse, press, true);
                yield return Send(mouse, barPoint, true);
                Assert.That(desktop.IsDragging, Is.True);
                yield return Send(mouse, barPoint, false);
                Assert.That(desktop.IsDragging, Is.False);
                Assert.That(desktop.CanPlace(icon), Is.True);
                Assert.That(source.GetSnapshot().Windows, Is.Empty);
                icon.Rect.localPosition = original;
                yield return Send(mouse, press, false);
                yield return Send(mouse, press, true);
                yield return Send(mouse, press + new Vector2(80, 0), true);
                Assert.That(desktop.IsDragging, Is.True);
                work.SetBarHeight(72);
                Assert.That(desktop.IsDragging, Is.False);
                Assert.That(icon.Rect.localPosition, Is.EqualTo(original));
                yield return Send(mouse, barPoint, false);
                Assert.That(source.GetSnapshot().Windows, Is.Empty);
            }
            finally
            {
                desktop.CancelInteraction();
                icon.Rect.localPosition = original;
                work.SetBarHeight(48);
                WindowManager.Instance.ResetWindows();
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator ActualWindowsRenderAndReceiveExclusiveInputAtBothResolutions()
        {
            var manager=WindowManager.Instance;
            var desktop=Object.FindAnyObjectByType<DesktopController>();
            var bar=Object.FindAnyObjectByType<TaskbarController>();
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var mouse=InputSystem.AddDevice<Mouse>("RealWindowMouse");
            var previous=module.actionsAsset.devices; module.actionsAsset.devices=new InputDevice[]{mouse};
            try
            {
                foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
                {
                    SetResolution(resolution.x,resolution.y); for(var f=0;f<5;f++) yield return null;
                    manager.ResetWindows();
                    var icon=desktop.Icons[1]; var ip=Center(icon.Rect);
                    yield return Send(mouse,ip,false); yield return Send(mouse,ip,true); yield return Send(mouse,ip,false);
                    yield return Send(mouse,ip,true); yield return Send(mouse,ip,false);
                    var w=manager.GetProcess("memo").Instance;
                    var id=manager.GetProcess("memo").InstanceId;
                    Assert.That(bar.Buttons.ContainsKey(id),Is.True);
                    var start=(Vector2)w.transform.position;
                    var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                    yield return Send(mouse,title,true); yield return Send(mouse,title+Vector2.right*3,true);
                    Assert.That(Vector2.Distance(w.transform.position,start),Is.LessThan(.001));
                    yield return Send(mouse,title+new Vector2(80,-40),true); yield return Send(mouse,title+new Vector2(80,-40),false);
                    Assert.That(Vector2.Distance(w.transform.position,start),Is.GreaterThan(.1));
                    var photo=manager.Open("photos"); photo.MoveTo((Vector2)w.transform.position+new Vector2(.5f,-.5f));
                    var selected=desktop.Selected;
                    var overlap=WorldPoint(photo.OuterBounds.center);
                    yield return Send(mouse,overlap,true); yield return Send(mouse,overlap,false);
                    Assert.That(manager.ActiveWindowId,Is.EqualTo(manager.GetProcess("photos").InstanceId));
                    Assert.That(desktop.Selected,Is.EqualTo(selected));
                    title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                    yield return Send(mouse,title,true); yield return Send(mouse,title,false);
                    Assert.That(manager.ActiveWindowId,Is.EqualTo(id));
                    var front=w.GetComponentsInChildren<Renderer>().Where(x=>x.sortingLayerName=="WindowFront").Min(x=>x.sortingOrder);
                    var back=photo.GetComponentsInChildren<Renderer>().Where(x=>x.sortingLayerName=="WindowFront").Max(x=>x.sortingOrder);
                    Assert.That(front,Is.GreaterThan(back));
                    var folder="Builds/DesktopValidation/WindowIntegration"; Directory.CreateDirectory(folder);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath($"{folder}/windows-{resolution.x}x{resolution.y}.png"));
                    for(var f=0;f<5;f++) yield return null;
                    manager.Close("photos");
                    foreach(ResizeDirection direction in Enum.GetValues(typeof(ResizeDirection)))
                    {
                        w.ApplyGeometry(Vector2.zero,new Vector2(4,3));
                        var bounds=w.OuterBounds;
                        var left=direction==ResizeDirection.Left||direction==ResizeDirection.TopLeft||direction==ResizeDirection.BottomLeft;
                        var right=direction==ResizeDirection.Right||direction==ResizeDirection.TopRight||direction==ResizeDirection.BottomRight;
                        var top=direction==ResizeDirection.Top||direction==ResizeDirection.TopLeft||direction==ResizeDirection.TopRight;
                        var bottom=direction==ResizeDirection.Bottom||direction==ResizeDirection.BottomLeft||direction==ResizeDirection.BottomRight;
                        var p=WorldPoint(new Vector2(left?bounds.xMin:right?bounds.xMax:bounds.center.x,top?bounds.yMax:bottom?bounds.yMin:bounds.center.y));
                        var delta=new Vector2(left?-30:right?30:0,top?30:bottom?-30:0);
                        yield return Send(mouse,p,false); yield return Send(mouse,p,true); yield return Send(mouse,p+delta,true); yield return Send(mouse,p+delta,false);
                        Assert.That(w.Size.x>=4&&w.Size.y>=3,Is.True,direction.ToString());
                        Assert.That(w.Size.x>4||w.Size.y>3,Is.True,direction.ToString());
                        if(!right) Assert.That(w.OuterBounds.xMax,Is.EqualTo(bounds.xMax).Within(.001));
                        if(!left) Assert.That(w.OuterBounds.xMin,Is.EqualTo(bounds.xMin).Within(.001));
                        if(!top) Assert.That(w.OuterBounds.yMax,Is.EqualTo(bounds.yMax).Within(.001));
                        if(!bottom) Assert.That(w.OuterBounds.yMin,Is.EqualTo(bounds.yMin).Within(.001));
                    }
                    w.ApplyGeometry(Vector2.zero,new Vector2(4,3));
                    var normalSize=w.Size; var normalPosition=w.transform.position;
                    var maximize=TitleButton(w,1);
                    yield return Send(mouse,maximize,true); Assert.That(w.IsMaximized,Is.False);
                    yield return Send(mouse,maximize,false); Assert.That(w.IsMaximized,Is.True);
                    Assert.That(w.OuterBounds.width,Is.EqualTo(DesktopWindowSpace.Instance.Bounds.width).Within(.001));
                    var point=Center((RectTransform)bar.Buttons[id].transform);
                    yield return Send(mouse,point,true); yield return Send(mouse,point,false);
                    Assert.That(manager.GetProcess("memo").State,Is.EqualTo(WindowState.Minimized));
                    yield return Send(mouse,point,true); yield return Send(mouse,point,false);
                    Assert.That(w.IsMaximized,Is.True);
                    maximize=TitleButton(w,1); yield return Send(mouse,maximize,true); yield return Send(mouse,maximize,false);
                    Assert.That(w.Size,Is.EqualTo(normalSize)); Assert.That(Vector2.Distance(w.transform.position,normalPosition),Is.LessThan(.001));
                    var close=TitleButton(w,2); yield return Send(mouse,close,true); Assert.That(manager.GetProcess("memo"),Is.Not.Null);
                    yield return Send(mouse,close+Vector2.down*60,true); yield return Send(mouse,close+Vector2.down*60,false);
                    Assert.That(manager.GetProcess("memo"),Is.Not.Null,"Dragging away must not close.");
                    close=TitleButton(w,2); yield return Send(mouse,close,true); yield return Send(mouse,close,false);
                    Assert.That(manager.GetProcess("memo"),Is.Null); Assert.That(bar.Buttons.ContainsKey(id),Is.False);
                }
            }
            finally { manager.ResetWindows(); module.actionsAsset.devices=previous; InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator InstanceIdentityPoliciesAndReentrantNotificationsSurviveReopen()
        {
            var manager=WindowManager.Instance;
            var source=Object.FindAnyObjectByType<WindowTaskbarSource>();
            var bar=Object.FindAnyObjectByType<TaskbarController>();
            var old=manager.Open("memo"); var oldId=manager.GetProcess("memo").InstanceId;
            var snapshot=source.GetSnapshot();
            manager.Close("memo"); var fresh=manager.Open("memo"); var freshId=manager.GetProcess("memo").InstanceId;
            old.RequestClose(); old.RequestMinimize(); old.RequestMaximize();
            Assert.That(manager.GetProcess("memo").Instance,Is.EqualTo(fresh));
            Assert.That(fresh.gameObject.activeSelf,Is.True); Assert.That(fresh.IsMaximized,Is.False);
            Assert.That(source.Request(oldId,TaskbarWindowAction.Activate),Is.False);
            yield return null; yield return null;
            Assert.That(manager.GetProcess("memo").InstanceId,Is.EqualTo(freshId));
            Assert.That(snapshot.Windows[0].WindowId,Is.EqualTo(oldId));
            var other=manager.Open("photos");
            manager.RegisterPrefab("warning",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Desktop/Windows/memo.prefab").GetComponent<W>());
            var modal=manager.Open("warning"); var modalId=manager.GetProcess("warning").InstanceId;
            manager.SetPolicy(modal,false,false,true,freshId);
            Assert.That(bar.Buttons.ContainsKey(modalId),Is.False);
            Assert.That(source.Request(freshId,TaskbarWindowAction.Minimize),Is.False);
            Assert.That(source.Request(freshId,TaskbarWindowAction.Activate),Is.True);
            Assert.That(manager.ActiveWindowId,Is.EqualTo(modalId));
            Assert.That(source.Request(manager.GetProcess("photos").InstanceId,TaskbarWindowAction.Activate),Is.False);
            manager.SetLocked(true); fresh.RequestClose();
            Assert.That(source.Request(freshId,TaskbarWindowAction.Activate),Is.False);
            Assert.That(manager.GetProcess("memo"),Is.Not.Null);
            manager.SetLocked(false); manager.Close("warning");
            manager.SetPolicy(fresh,true,false); fresh.RequestMinimize(); Assert.That(fresh.gameObject.activeSelf,Is.True);
            Object.Destroy(fresh.gameObject); yield return null; yield return null;
            Assert.That(manager.GetProcess("memo"),Is.Null); Assert.That(bar.Buttons.ContainsKey(freshId),Is.False);
            manager.ResetWindows();
            bool reopen=true;
            Action<WindowProcess> onOpen=p=> { if(p.Id=="memo"&&reopen) { reopen=false; manager.Close("memo"); manager.Open("memo"); } };
            manager.OnProcessOpened+=onOpen;
            manager.Open("memo"); manager.OnProcessOpened-=onOpen;
            Assert.That(manager.ActiveProcesses.Count,Is.EqualTo(1));
            Assert.That(source.GetSnapshot().ActiveWindowId,Is.EqualTo(manager.GetProcess("memo").InstanceId));
            yield return null; yield return null;
            Assert.That(bar.Buttons.Count,Is.EqualTo(1));
            manager.ResetWindows();
        }

        [UnityTest]
        public IEnumerator WheelAndProjectionChangesRespectCapturedInput()
        {
            var manager=WindowManager.Instance;
            var bar=Object.FindAnyObjectByType<TaskbarController>();
            var desktop=Object.FindAnyObjectByType<DesktopController>();
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var mouse=InputSystem.AddDevice<Mouse>("CaptureWindowMouse");
            var devices=module.actionsAsset.devices; module.actionsAsset.devices=new InputDevice[]{mouse};
            var camera=DesktopWindowSpace.Instance.Camera; var originalSize=camera.orthographicSize;
            try
            {
                for(var i=0;i<16;i++) { manager.RegisterPrefab("wheel"+i,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Desktop/Windows/memo.prefab").GetComponent<W>()); manager.Open("wheel"+i); }
                yield return null; yield return null;
                Assert.That(bar.Overflow,Is.True);
                var point=Center(bar.Viewport); yield return Send(mouse,point,false);
                var before=bar.Content.anchoredPosition.x;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,-120)});
                yield return null; yield return null;
                Assert.That(bar.Content.anchoredPosition.x,Is.GreaterThan(before),"Wheel must work without PointerDown.");
                var blank=new Vector2(Screen.width-50,Screen.height-50);
                yield return Send(mouse,blank,false); yield return Send(mouse,blank,true); yield return Send(mouse,point,true);
                Assert.That(desktop.IsSelecting,Is.True,Diagnostic());
                before=bar.Content.anchoredPosition.x;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,120)}.WithButton(MouseButton.Left,true));
                yield return null; yield return null;
                Assert.That(bar.Content.anchoredPosition.x,Is.EqualTo(before).Within(.01));
                yield return Send(mouse,point,false);
                manager.ResetWindows(); var w=manager.Open("memo");
                var initial=w.transform.position;
                var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                yield return Send(mouse,title,true); yield return Send(mouse,title+new Vector2(70,-30),true);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Moving));
                var bounds=DesktopWindowSpace.Instance.Bounds;
                camera.orthographicSize*=.9f; DesktopWindowSpace.Instance.Refresh();
                Assert.That(DesktopWindowSpace.Instance.Bounds,Is.Not.EqualTo(bounds));
                Assert.That(Vector2.Distance(w.transform.position,initial),Is.LessThan(.001));
                yield return Send(mouse,title+new Vector2(70,-30),true);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.None));
                yield return Send(mouse,TitleButton(w,2),false);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null,"Cancelled release must not close a window.");
            }
            finally { camera.orthographicSize=originalSize; manager.ResetWindows(); module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); }
        }
        [UnityTest]
        public IEnumerator SecondaryCaptureIncludesBackAndExcludesFrontAndTaskbar()
        {
            SetResolution(1280,720); for(var i=0;i<5;i++) yield return null;
            var manager=WindowManager.Instance; var w=manager.Open("memo");
            w.SetSecondarySprite(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/WindowWhite.png"));
            w.transform.Find("Secondary").GetComponent<SpriteRenderer>().color=Color.green;
            w.transform.Find("Primary/Background").GetComponent<SpriteRenderer>().color=Color.red;
            for(var i=0;i<5;i++) yield return null;
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Tests/Editor/Desktop/SecondaryCaptureProbe.shader");
            var probes=new List<GameObject>(); var materials=new List<Material>();
            var sample=DesktopWindowSpace.Instance.Camera.WorldToViewportPoint(w.transform.position+Vector3.down);
            var samples=new[]{new Vector2(sample.x,sample.y),new Vector2(.9f,.01f)};
            try
            {
                for(var i=0;i<2;i++)
                {
                    var go=new GameObject("CaptureConsumer"+i); probes.Add(go); go.transform.position=new Vector3(7,i,0);
                    var r=go.AddComponent<SpriteRenderer>(); r.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Screen/WindowWhite.png");
                    var mat=new Material(shader); materials.Add(mat); mat.SetVector("_SampleUV",new Vector4(samples[i].x,samples[i].y,0,0));
                    r.sharedMaterial=mat; r.sortingLayerName="WindowFront"; r.sortingOrder=20000;
                }
                for(var i=0;i<5;i++) yield return null;
                var path="Builds/DesktopValidation/WindowIntegration/secondary-capture.png";
                ScreenCapture.CaptureScreenshot(path); for(var i=0;i<5;i++) yield return null;
                var pixels=new Texture2D(2,2); pixels.LoadImage(File.ReadAllBytes(path));
                try
                {
                    var a=WorldPoint(new Vector2(7,0)); var b=WorldPoint(new Vector2(7,1));
                    var back=pixels.GetPixel((int)a.x,(int)a.y); var underBar=pixels.GetPixel((int)b.x,(int)b.y);
                    Assert.That(back.g,Is.GreaterThan(.8f),back.ToString()); Assert.That(back.r,Is.LessThan(.2f),"Front must not appear in the capture.");
                    Assert.That(underBar.b,Is.GreaterThan(.7f),underBar.ToString());
                }
                finally { Object.Destroy(pixels); }
            }
            finally { foreach(var go in probes) Object.Destroy(go); foreach(var mat in materials) Object.Destroy(mat); manager.ResetWindows(); }
        }
        [UnityTest]
        public IEnumerator SmallAreasRestoreNormalGeometryAndEscapeConsumesRelease()
        {
            SetResolution(1024,768); for(var i=0;i<5;i++) yield return null;
            var manager=WindowManager.Instance; var w=manager.Open("memo");
            var space=DesktopWindowSpace.Instance; var camera=space.Camera; var originalSize=camera.orthographicSize;
            var normal=w.Size; var position=w.transform.position;
            camera.orthographicSize=1; space.Refresh();
            Assert.That(w.OuterBounds.width,Is.LessThanOrEqualTo(space.Bounds.width+.001f));
            Assert.That(w.OuterBounds.height,Is.LessThanOrEqualTo(space.Bounds.height+.001f));
            camera.orthographicSize=originalSize; space.Refresh();
            Assert.That(w.Size,Is.EqualTo(normal)); Assert.That(w.transform.position,Is.EqualTo(position));
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("CancelWindowMouse"); var keyboard=InputSystem.AddDevice<Keyboard>("CancelWindowKeyboard");
            module.actionsAsset.devices=new InputDevice[]{mouse,keyboard};
            try
            {
                var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                yield return Send(mouse,title,false); yield return Send(mouse,title,true); yield return Send(mouse,title+new Vector2(80,-20),true);
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.Moving));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(w.CurrentInteraction,Is.EqualTo(WindowInteraction.None)); Assert.That(w.transform.position,Is.EqualTo(position));
                yield return Send(mouse,TitleButton(w,2),true); yield return Send(mouse,TitleButton(w,2),false);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null; yield return null;
                title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                yield return Send(mouse,title,true); yield return Send(mouse,title+new Vector2(80,-20),true);
                DesktopInputRouter.Instance.SendMessage("OnApplicationFocus",false);
                Assert.That(w.transform.position,Is.EqualTo(position));
                DesktopInputRouter.Instance.SendMessage("OnApplicationFocus",true);
                yield return Send(mouse,TitleButton(w,2),true); yield return Send(mouse,TitleButton(w,2),false);
                Assert.That(manager.GetProcess("memo"),Is.Not.Null);
            }
            finally { camera.orthographicSize=originalSize; manager.ResetWindows(); module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); }
        }
        [UnityTest]
        public IEnumerator CoalescedReleaseAndAdapterDisablePreserveWindowState()
        {
            // This case assumes no viewport cancellation during the coalesced event pair.
            SetResolution(1280,720);
            for(var frame=0;frame<4;frame++) yield return null;
            var manager=WindowManager.Instance; var w=manager.Open("memo");
            var source=Object.FindAnyObjectByType<WindowTaskbarSource>(); var bar=Object.FindAnyObjectByType<TaskbarController>();
            var id=manager.GetProcess("memo").InstanceId;
            source.enabled=false; Assert.That(bar.Buttons,Is.Empty); Assert.That(source.GetSnapshot().ActiveWindowId,Is.Null);
            Assert.That(source.Request(id,TaskbarWindowAction.Minimize),Is.False);
            source.enabled=true; Assert.That(bar.Buttons.Count,Is.EqualTo(1));
            var module=Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var devices=module.actionsAsset.devices; var mouse=InputSystem.AddDevice<Mouse>("CoalescedWindowMouse"); module.actionsAsset.devices=new InputDevice[]{mouse};
            try
            {
                var title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                var before=w.transform.position;
                yield return Send(mouse,title,false); yield return Send(mouse,title,true);
                yield return Send(mouse,title+new Vector2(100,-30),false);
                Assert.That(Vector2.Distance(w.transform.position,before),Is.GreaterThan(.1),"Final release position must be applied even without an intermediate held frame.");
                before=w.transform.position;
                title=WorldPoint(new Vector2(w.OuterBounds.xMin+.6f,w.OuterBounds.yMax-w.BarHeight/2));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=title}.WithButton(MouseButton.Left,true));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=title+new Vector2(70,-20)});
                yield return null; yield return null;
                Assert.That(Vector2.Distance(w.transform.position,before),Is.GreaterThan(.1),"Press coordinates must survive a down/up pair processed in one update. "+Diagnostic());
                Object.Destroy(manager.gameObject); yield return null; yield return null;
                Assert.That(bar.Buttons,Is.Empty,"Destroying the provider must clear its displayed buttons.");
            }
            finally { module.actionsAsset.devices=devices; InputSystem.RemoveDevice(mouse); }
        }
        private static Vector2 WorldPoint(Vector2 world) => DesktopWindowSpace.Instance.Camera.WorldToScreenPoint(new Vector3(world.x,world.y,0));
        private static Vector2 TitleButton(W w,int button) => WorldPoint(new Vector2(w.OuterBounds.xMax-1.05f+button*.4f,w.OuterBounds.yMax-w.BarHeight/2));
        private static string Diagnostic() => $"Owner={DesktopInputRouter.Instance.Owner}, {DesktopInputRouter.Instance.Diagnostics}, Mouse={Mouse.current?.name}, Space={DesktopWindowSpace.Instance.Valid}/{DesktopWindowSpace.Instance.Bounds}, Cam={DesktopWindowSpace.Instance.Camera.transform.position}";
        private static Camera EventCamera(Transform t) { var c=t.GetComponentInParent<Canvas>(); return c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera; }

        private static Vector2 Center(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(EventCamera(rect), rect.TransformPoint(rect.rect.center));

        private static Vector2 ScreenPoint(DesktopController desktop, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(EventCamera(desktop.IconArea), desktop.IconArea.TransformPoint(local));

        private static IEnumerator Send(Mouse mouse, Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            yield return null;
            yield return null;
        }
    }
}
