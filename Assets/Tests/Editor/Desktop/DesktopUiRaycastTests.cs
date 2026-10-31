using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace WindowExe.Desktop.Tests
{
    public sealed class DesktopUiRaycastTests
    {
        private GameObject root;
        private DesktopController desktop;
        private DesktopIconView icon;
        private DesktopItemDefinition definition;
        private EventSystem events;
        private RectTransform windowLayer;
        private DesktopBackground background;
        private DesktopSelectionBox selectionBox;
        private readonly List<DesktopItemDefinition> extraDefinitions = new List<DesktopItemDefinition>();
        private EventSystem[] previousEventSystems;
        private DesktopInputTestScope inputScope;
        private Canvas[] previousCanvases;
        private DesktopInputRouter previousRouter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            inputScope = new DesktopInputTestScope();
            previousCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach(var c in previousCanvases) c.enabled=false;
            previousRouter=Object.FindAnyObjectByType<DesktopInputRouter>();
            if(previousRouter!=null) previousRouter.enabled=false;
            previousEventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var previous in previousEventSystems) previous.enabled = false;
            root = new GameObject("RaycastTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            desktop = root.AddComponent<DesktopController>();
            var backdrop = Panel("Background", root.transform, true);
            background = backdrop.gameObject.AddComponent<DesktopBackground>();
            background.Configure(desktop);
            var area = Panel("Icons", root.transform, true);
            Object.DestroyImmediate(area.GetComponent<Image>());
            windowLayer = Panel("Windows", root.transform, true);
            Object.DestroyImmediate(windowLayer.GetComponent<Image>());
            desktop.Configure(area, windowLayer);
            selectionBox = DesktopSelectionBox.Create(area);
            desktop.ConfigureSelectionBox(selectionBox);
            var iconRect = Panel("Icon", area, false);
            iconRect.sizeDelta = new Vector2(100, 120);
            iconRect.anchoredPosition = new Vector2(20, -20);
            definition = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            definition.Configure("memo", "메모.txt", null);
            icon = iconRect.gameObject.AddComponent<DesktopIconView>();
            icon.Configure(desktop, definition, iconRect.GetComponent<Image>(), null, null);
            events = new GameObject("Events", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
            foreach (var item in extraDefinitions) Object.DestroyImmediate(item);
            extraDefinitions.Clear();
            if (events != null && events.name == "Events") Object.DestroyImmediate(events.gameObject);
            foreach (var previous in previousEventSystems)
                if (previous != null) previous.enabled = true;
            foreach(var c in previousCanvases) if(c!=null) c.enabled=true;
            if(previousRouter!=null) previousRouter.enabled=true;
            inputScope?.Dispose();
            inputScope = null;
            yield return new ExitPlayMode();
        }

        private static RectTransform Panel(string name, Transform parent, bool stretch)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0, 1);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            return rect;
        }

        private GameObject Hit(Vector2 screenPosition)
        {
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(events) { position = screenPosition };
            var hits = new List<RaycastResult>();
            events.RaycastAll(data, hits);
            return hits.Count == 0 ? null : hits[0].gameObject;
        }

        private Vector2 ScreenPoint(Vector2 point) =>
            RectTransformUtility.WorldToScreenPoint(null, desktop.IconArea.TransformPoint(point));

        private string InputState(Mouse mouse)
        {
            var module = events.GetComponent<InputSystemUIInputModule>();
            return $"Focus={Application.isFocused}, EventSystem={events.isActiveAndEnabled}, Current={events.currentInputModule}, " +
                $"Mouse={mouse.position.ReadValue()}, Enabled={mouse.enabled}, Point={module.point.action.ReadValue<Vector2>()}, " +
                $"PointEnabled={module.point.action.enabled}, ClickEnabled={module.leftClick.action.enabled}, " +
                $"Hit={Hit(mouse.position.ReadValue())}, Selected={desktop.Selected}, Selecting={desktop.IsSelecting}";
        }

        private DesktopIconView ExtraIcon()
        {
            var rect = Panel("SecondIcon", desktop.IconArea, false);
            rect.sizeDelta = new Vector2(100, 120);
            rect.anchoredPosition = new Vector2(160, -20);
            var item = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            item.Configure("photos", "사진", null);
            extraDefinitions.Add(item);
            var view = rect.gameObject.AddComponent<DesktopIconView>();
            view.Configure(desktop, item, rect.GetComponent<Image>(), null, null);
            return view;
        }

        [UnityTest]
        public IEnumerator InputModuleRegionSelectionAndGroupDragRespectCanvasScale()
        {
            var second = ExtraIcon();
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var module = events.GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("RegionTestMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var opened = 0;
            desktop.OpenRequested += _ => opened++;
            try
            {
                foreach (var scale in new[] { 0.5f, 1.5f })
                {
                    scaler.scaleFactor = scale;
                    icon.Rect.anchoredPosition = new Vector2(20, -20);
                    second.Rect.anchoredPosition = new Vector2(160, -20);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    var start = ScreenPoint(new Vector2(5, -5));
                    var end = ScreenPoint(new Vector2(300, -160));
                    Assert.That(Hit(start), Is.EqualTo(background.gameObject));
                    yield return SendMouse(mouse, start, false);
                    yield return SendMouse(mouse, start, true);
                    yield return SendMouse(mouse, end, true);
                    Assert.That(desktop.IsSelecting, Is.True, InputState(mouse));
                    Assert.That(selectionBox.Visible, Is.True);
                    Assert.That(desktop.SelectedIcons, Is.EquivalentTo(new[] { icon, second }));
                    Assert.That(Hit(ScreenPoint(new Vector2(70, -80))), Is.EqualTo(icon.gameObject));
                    yield return SendMouse(mouse, end, false);
                    Assert.That(desktop.IsSelecting, Is.False);
                    Assert.That(selectionBox.Visible, Is.False);
                    Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                    var press = ScreenPoint(new Vector2(70, -80));
                    var target = ScreenPoint(new Vector2(110, -150));
                    yield return SendMouse(mouse, press, false);
                    yield return SendMouse(mouse, press, true);
                    Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                    yield return SendMouse(mouse, target, true);
                    yield return SendMouse(mouse, target, false);
                    Assert.That(icon.Rect.anchoredPosition.x, Is.EqualTo(60).Within(0.1));
                    Assert.That(icon.Rect.anchoredPosition.y, Is.EqualTo(-90).Within(0.1));
                    Assert.That(second.Rect.anchoredPosition - icon.Rect.anchoredPosition, Is.EqualTo(new Vector2(140, 0)));
                    Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                    // A tiny background movement is a click, not a new region.
                    yield return SendMouse(mouse, start, true);
                    yield return SendMouse(mouse, start + Vector2.one, true);
                    Assert.That(desktop.IsSelecting, Is.False);
                    yield return SendMouse(mouse, start + Vector2.one, false);
                    Assert.That(desktop.SelectedIcons, Is.Empty);
                    Assert.That(opened, Is.Zero);
                }
            }
            finally
            {
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator WindowDragDoesNotStartRegionButBackgroundDragContinuesAcrossWindow()
        {
            var blocker = Panel("WindowBlocker", windowLayer, false);
            blocker.anchoredPosition = new Vector2(150, -20);
            blocker.sizeDelta = new Vector2(180, 180);
            var module = events.GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("WindowRegionTestMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                var start = ScreenPoint(new Vector2(200, -50));
                var end = ScreenPoint(new Vector2(300, -160));
                yield return SendMouse(mouse, start, false);
                yield return SendMouse(mouse, start, true);
                yield return SendMouse(mouse, end, true);
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(selectionBox.Visible, Is.False);
                yield return SendMouse(mouse, end, false);
                start = ScreenPoint(new Vector2(5, -5));
                yield return SendMouse(mouse, start, false);
                yield return SendMouse(mouse, start, true);
                yield return SendMouse(mouse, end, true);
                Assert.That(desktop.IsSelecting, Is.True);
                Assert.That(selectionBox.Visible, Is.True);
                Assert.That(Hit(end), Is.EqualTo(blocker.gameObject));
                Assert.That(selectionBox.transform.GetSiblingIndex(), Is.LessThan(windowLayer.GetSiblingIndex()));
                Assert.That(desktop.Selected, Is.EqualTo(icon), InputState(mouse));
                yield return SendMouse(mouse, end, false);
                Assert.That(desktop.Selected, Is.EqualTo(icon));
                Assert.That(selectionBox.Visible, Is.False);
            }
            finally
            {
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator EscapeDisableAndResizeCancelWithoutLeavingSelectionBoxOrPartialMove()
        {
            var second = ExtraIcon();
            var initialFirstPosition = icon.Rect.anchoredPosition;
            var initialSecondPosition = second.Rect.anchoredPosition;
            var module = events.GetComponent<InputSystemUIInputModule>();
            var mouse = InputSystem.AddDevice<Mouse>("CancelRegionMouse");
            var keyboard = InputSystem.AddDevice<Keyboard>("CancelRegionKeyboard");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse, keyboard };
            try
            {
                desktop.Select(icon);
                var start = ScreenPoint(new Vector2(5, -5));
                var end = ScreenPoint(new Vector2(300, -160));
                yield return SendMouse(mouse, start, false);
                yield return SendMouse(mouse, start, true);
                yield return SendMouse(mouse, end, true);
                Assert.That(desktop.SelectedIcons.Count, Is.EqualTo(2));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(selectionBox.Visible, Is.False);
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
                yield return SendMouse(mouse, end, false);
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                var data = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
                    pressPosition = start, position = end };
                desktop.BeginSelection(data);
                desktop.EndSelection(data);
                var drag = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
                    pressPosition = ScreenPoint(new Vector2(70, -80)), position = ScreenPoint(new Vector2(100, -120)) };
                desktop.BeginDrag(icon, drag);
                second.gameObject.SetActive(false);
                Assert.That(desktop.IsDragging, Is.False);
                Assert.That(icon.Rect.anchoredPosition, Is.EqualTo(initialFirstPosition));
                Assert.That(second.Rect.anchoredPosition, Is.EqualTo(initialSecondPosition));
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
                second.gameObject.SetActive(true);
                desktop.BeginSelection(data);
                desktop.enabled = false;
                Assert.That(selectionBox.Visible, Is.False);
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
                desktop.enabled = true;
                desktop.BeginSelection(data);
                desktop.IconArea.offsetMax += new Vector2(-10, -10);
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(selectionBox.Visible, Is.False);
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
                var scaler = root.AddComponent<CanvasScaler>();
                yield return null;
                yield return null;
                desktop.BeginSelection(data);
                scaler.scaleFactor = 0.8f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                Assert.That(desktop.IsSelecting, Is.False);
                Assert.That(selectionBox.Visible, Is.False);
                Assert.That(desktop.SelectedIcons, Is.EqualTo(new[] { icon }));
            }
            finally
            {
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator EmptyWindowLayerDoesNotBlockIconsAndCoveringWindowDoes()
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, icon.Rect.TransformPoint(icon.Rect.rect.center));
            Assert.That(Hit(point), Is.EqualTo(icon.gameObject),
                $"Point={point}, Screen={Screen.width}x{Screen.height}, Area={desktop.IconArea.rect}, Icon={icon.Rect.anchoredPosition}");
            desktop.Select(icon);
            var blocker = Panel("WindowBlocker", windowLayer, false);
            blocker.anchoredPosition = new Vector2(20, -20);
            blocker.sizeDelta = new Vector2(100, 120);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(Hit(point), Is.EqualTo(blocker.gameObject));
            ExecuteEvents.ExecuteHierarchy(Hit(point), new PointerEventData(events) { position = point },
                ExecuteEvents.pointerClickHandler);
            Assert.That(desktop.Selected, Is.EqualTo(icon));
            blocker.gameObject.SetActive(false);
            Assert.That(Hit(point), Is.EqualTo(icon.gameObject));
            yield break;
        }

        [UnityTest]
        public IEnumerator EmptyDesktopClickClearsSelectionWithoutCreatingOpenRequest()
        {
            desktop.Select(icon);
            var opened = 0;
            desktop.OpenRequested += _ => opened++;
            var point = RectTransformUtility.WorldToScreenPoint(null,
                desktop.IconArea.TransformPoint(desktop.IconArea.rect.center));
            var hit = Hit(point);
            Assert.That(hit.name, Is.EqualTo("Background"));
            ExecuteEvents.ExecuteHierarchy(hit, new PointerEventData(events) { position = point },
                ExecuteEvents.pointerClickHandler);
            Assert.That(desktop.Selected, Is.Null);
            Assert.That(opened, Is.Zero);
            yield break;
        }

        [UnityTest]
        public IEnumerator InputModuleHandlesSmallMovementAndDragWithoutAccidentalOpen()
        {
            var module = events.GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            var mouse = InputSystem.AddDevice<Mouse>("DesktopTestMouse");
            var previousDevices = module.actionsAsset.devices;
            module.actionsAsset.devices = new InputDevice[] { mouse };
            var original = icon.Rect.anchoredPosition;
            var point = RectTransformUtility.WorldToScreenPoint(null, icon.Rect.TransformPoint(icon.Rect.rect.center));
            var opened = 0;
            desktop.OpenRequested += _ => opened++;
            try
            {
                yield return SendMouse(mouse, point, false);
                yield return SendMouse(mouse, point, true);
                yield return SendMouse(mouse, point + Vector2.one, true);
                yield return SendMouse(mouse, point + Vector2.one, false);
                Assert.That(desktop.Selected, Is.EqualTo(icon));
                Assert.That(icon.Rect.anchoredPosition, Is.EqualTo(original));
                Assert.That(opened, Is.Zero);
                yield return SendMouse(mouse, point, true);
                yield return SendMouse(mouse, point + new Vector2(80, 0), true);
                yield return SendMouse(mouse, point + new Vector2(80, 0), false);
                Assert.That(icon.Rect.anchoredPosition.x, Is.GreaterThan(original.x));
                Assert.That(opened, Is.Zero);
            }
            finally
            {
                module.actionsAsset.devices = previousDevices;
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator SendMouse(Mouse mouse, Vector2 point, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left, pressed));
            // Let both the Input System and EventSystem complete an Update before asserting.
            yield return null;
            yield return null;
        }
    }
}
