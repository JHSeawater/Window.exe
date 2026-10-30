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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            root = new GameObject("RaycastTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            desktop = root.AddComponent<DesktopController>();
            var background = Panel("Background", root.transform, true);
            background.gameObject.AddComponent<DesktopBackground>().Configure(desktop);
            var area = Panel("Icons", root.transform, true);
            Object.DestroyImmediate(area.GetComponent<Image>());
            windowLayer = Panel("Windows", root.transform, true);
            Object.DestroyImmediate(windowLayer.GetComponent<Image>());
            desktop.Configure(area, windowLayer);
            var iconRect = Panel("Icon", area, false);
            iconRect.sizeDelta = new Vector2(100, 120);
            iconRect.anchoredPosition = new Vector2(20, -20);
            definition = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            definition.Configure("memo", "메모.txt", null);
            icon = iconRect.gameObject.AddComponent<DesktopIconView>();
            icon.Configure(desktop, definition, iconRect.GetComponent<Image>(), null, null);
            events = Object.FindFirstObjectByType<EventSystem>();
            if (events == null)
                events = new GameObject("Events", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
            if (events != null && events.name == "Events") Object.DestroyImmediate(events.gameObject);
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
            var data = new PointerEventData(events) { position = screenPosition };
            var hits = new List<RaycastResult>();
            events.RaycastAll(data, hits);
            return hits.Count == 0 ? null : hits[0].gameObject;
        }

        [UnityTest]
        public IEnumerator EmptyWindowLayerDoesNotBlockIconsAndCoveringWindowDoes()
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, icon.Rect.TransformPoint(icon.Rect.rect.center));
            Assert.That(Hit(point), Is.EqualTo(icon.gameObject));
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
            yield return null;
        }
    }
}
