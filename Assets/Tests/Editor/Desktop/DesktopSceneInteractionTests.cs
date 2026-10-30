using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

namespace WindowExe.Desktop.Tests
{
    public sealed class DesktopSceneInteractionTests
    {
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
                    desktop.ClearSelection();
                    var first = desktop.Icons[0];
                    var second = desktop.Icons[1];
                    var bounds = DesktopLayout.Union(DesktopLayout.BoundsInParent(first.Rect, desktop.IconArea),
                        DesktopLayout.BoundsInParent(second.Rect, desktop.IconArea));
                    var start = ScreenPoint(desktop, new Vector2(bounds.xMin - 8, bounds.yMax + 8));
                    var end = ScreenPoint(desktop, new Vector2(bounds.xMax + 8, bounds.yMin - 8));
                    yield return Send(mouse, start, false);
                    yield return Send(mouse, start, true);
                    yield return Send(mouse, end, true);
                    Assert.That(desktop.IsSelecting, Is.True);
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
                    var press = RectTransformUtility.WorldToScreenPoint(null, first.Rect.TransformPoint(first.Rect.rect.center));
                    Vector2 movement = new Vector2(220, -30);
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
            var bar = desktop.GetComponentInChildren<TaskbarController>();
            var source = desktop.GetComponent<TaskbarDemoSource>();
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
                    source.Model.Reset();
                    var icon = desktop.Icons[0];
                    var iconPoint = RectTransformUtility.WorldToScreenPoint(null, icon.Rect.TransformPoint(icon.Rect.rect.center));
                    yield return Send(mouse, iconPoint, false);
                    yield return Send(mouse, iconPoint, true);
                    yield return Send(mouse, iconPoint, false);
                    yield return Send(mouse, iconPoint, true);
                    yield return Send(mouse, iconPoint, false);
                    Assert.That(source.GetSnapshot().Windows.Count, Is.EqualTo(1), "Double-click must reach the shared demo adapter once.");
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
                    Assert.That(source.Model.Find(id).IsMinimized, Is.True);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    Assert.That(source.Model.Find(id).IsMinimized, Is.False);
                    source.Model.SetMaximized(id, true);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    yield return Send(mouse, point, true);
                    yield return Send(mouse, point, false);
                    Assert.That(source.Model.Find(id).IsMaximized, Is.True);
                    Assert.That(source.Model.Find(id).IsMinimized, Is.False);
                    bar.enabled = false;
                    Assert.That(bar.Buttons, Is.Empty);
                    var second = source.Model.Open("photos", "사진");
                    bar.enabled = true;
                    yield return null;
                    yield return null;
                    Assert.That(bar.Buttons.Count, Is.EqualTo(2));
                    source.Model.Close(second);
                    Assert.That(bar.Buttons.ContainsKey(second), Is.False);
                    for (var i = 0; i < 18; i++) source.Model.Open("extra" + i, $"기록 {i} - 아주 긴 창 제목이 영역을 벗어나지 않아야 합니다");
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
                    Assert.That(source.Model.Find(active).IsMinimized, Is.False);
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
                source.Model.Reset();
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator TaskbarInputDoesNotLeakAndDesktopDragsCanEndOverBar()
        {
            var desktop = Object.FindAnyObjectByType<DesktopController>();
            var bar = desktop.GetComponentInChildren<TaskbarController>();
            var source = desktop.GetComponent<TaskbarDemoSource>();
            var work = desktop.GetComponent<DesktopWorkArea>();
            var module = Object.FindAnyObjectByType<EventSystem>().GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("TaskbarDirectionMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var icon = desktop.Icons[0];
            var original = icon.Rect.localPosition;
            try
            {
                source.Model.Reset();
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
                Assert.That(desktop.IsSelecting, Is.True);
                Assert.That(desktop.SelectionBounds.yMin, Is.GreaterThanOrEqualTo(desktop.IconArea.rect.yMin - 0.1f));
                yield return Send(mouse, barPoint, false);
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(source.GetSnapshot().Windows, Is.Empty);
                desktop.ClearSelection();
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
                source.Model.Reset();
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static Vector2 Center(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        private static Vector2 ScreenPoint(DesktopController desktop, Vector2 local) =>
            RectTransformUtility.WorldToScreenPoint(null, desktop.IconArea.TransformPoint(local));

        private static IEnumerator Send(Mouse mouse, Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            yield return null;
            yield return null;
        }
    }
}
