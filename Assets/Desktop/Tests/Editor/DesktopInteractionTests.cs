using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WindowExe.Desktop.Tests
{
    public sealed class DesktopInteractionTests
    {
        private GameObject root;
        private RectTransform area;
        private DesktopController desktop;
        private readonly List<DesktopItemDefinition> definitions = new List<DesktopItemDefinition>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DesktopTest", typeof(RectTransform));
            area = (RectTransform)root.transform;
            area.sizeDelta = new Vector2(800, 600);
            area.pivot = new Vector2(0, 1);
            desktop = root.AddComponent<DesktopController>();
            desktop.Configure(area, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (var definition in definitions) Object.DestroyImmediate(definition);
            definitions.Clear();
        }

        private DesktopIconView Icon(string id, Vector2 position)
        {
            var definition = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            definition.Configure(id, id, null);
            definitions.Add(definition);
            var obj = new GameObject(id, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(area, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(120, 140);
            rect.anchoredPosition = position;
            var view = obj.AddComponent<DesktopIconView>();
            view.Configure(desktop, definition, obj.GetComponent<Image>(), null, null);
            return view;
        }

        private static PointerEventData Pointer(Vector2 press, Vector2 current)
        {
            return new PointerEventData(null) { button = PointerEventData.InputButton.Left,
                pressPosition = press, position = current, eligibleForClick = true };
        }

        [Test]
        public void SelectionChangesAndBlankClickClearsIt()
        {
            var first = Icon("first", new Vector2(20, -20));
            var second = Icon("second", new Vector2(180, -20));
            first.OnPointerDown(Pointer(Vector2.zero, Vector2.zero));
            Assert.That(desktop.Selected, Is.EqualTo(first));
            second.OnPointerDown(Pointer(Vector2.zero, Vector2.zero));
            Assert.That(first.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(desktop.Selected, Is.EqualTo(second));
            var background = root.AddComponent<DesktopBackground>();
            background.Configure(desktop);
            background.OnPointerClick(Pointer(Vector2.zero, Vector2.zero));
            Assert.That(desktop.Selected, Is.Null);
        }

        [Test]
        public void DoubleClickUsesStableIdAndFiresOnlyOncePerPair()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            var received = new List<string>();
            desktop.OpenRequested += received.Add;
            icon.SetDisplayName("changed.txt");
            desktop.Click(icon, new Vector2(10, 10), 1);
            desktop.Click(icon, new Vector2(12, 11), 1.1f);
            desktop.Click(icon, new Vector2(12, 11), 1.2f);
            Assert.That(received, Is.EqualTo(new[] { "memo" }));
        }

        [Test]
        public void DifferentIconsOrSlowClicksDoNotOpen()
        {
            var first = Icon("first", new Vector2(20, -20));
            var second = Icon("second", new Vector2(180, -20));
            var count = 0;
            desktop.OpenRequested += _ => count++;
            desktop.Click(first, Vector2.zero, 1);
            desktop.Click(second, Vector2.zero, 1.1f);
            desktop.Click(second, Vector2.zero, 2);
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void DragPreservesGrabOffsetIncludingMovementBeforeThreshold()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            var press = new Vector2(40, -50);
            var data = Pointer(press, press + new Vector2(100, -60));
            icon.OnPointerDown(Pointer(press, press));
            icon.OnBeginDrag(data);
            Assert.That(icon.Rect.anchoredPosition.x, Is.EqualTo(120).Within(0.01));
            Assert.That(icon.Rect.anchoredPosition.y, Is.EqualTo(-80).Within(0.01));
            Assert.That(data.eligibleForClick, Is.False);
            icon.OnEndDrag(data);
        }

        [TestCase(0.5f)]
        [TestCase(1.5f)]
        public void DragConvertsScaledScreenMovement(float scale)
        {
            area.localScale = Vector3.one * scale;
            var icon = Icon("memo", new Vector2(20, -20));
            var press = new Vector2(40, -50) * scale;
            var data = Pointer(press, press + new Vector2(100, -60) * scale);
            icon.OnBeginDrag(data);
            Assert.That(icon.Rect.anchoredPosition.x, Is.EqualTo(120).Within(0.01));
            Assert.That(icon.Rect.anchoredPosition.y, Is.EqualTo(-80).Within(0.01));
            icon.OnEndDrag(data);
        }

        [Test]
        public void OverlappingDropReturnsToOriginalPosition()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            Icon("photos", new Vector2(200, -20));
            var data = Pointer(new Vector2(30, -30), new Vector2(210, -30));
            icon.OnBeginDrag(data);
            Assert.That(desktop.EndDrag(icon), Is.False);
            Assert.That(icon.Rect.anchoredPosition, Is.EqualTo(new Vector2(20, -20)));
        }

        [Test]
        public void DragClampsCompleteIconAndNameInsideArea()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            var data = Pointer(new Vector2(30, -30), new Vector2(10000, -10000));
            icon.OnBeginDrag(data);
            Assert.That(icon.Rect.anchoredPosition.x, Is.EqualTo(680).Within(0.01));
            Assert.That(icon.Rect.anchoredPosition.y, Is.EqualTo(-460).Within(0.01));
            Assert.That(desktop.EndDrag(icon), Is.True);
        }

        [Test]
        public void DragCannotProduceDoubleClickAndNextSingleClickDoesNotOpen()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            var count = 0;
            desktop.OpenRequested += _ => count++;
            desktop.Click(icon, Vector2.zero, 0);
            var data = Pointer(new Vector2(30, -30), new Vector2(50, -50));
            icon.OnPointerDown(data);
            icon.OnBeginDrag(data);
            icon.OnEndDrag(data);
            icon.OnPointerClick(data);
            desktop.Click(icon, Vector2.zero, 0.1f);
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void InitialOverlapsAreResolvedAndDuplicateIdsRejected()
        {
            var first = Icon("first", new Vector2(20, -20));
            var second = Icon("second", new Vector2(20, -20));
            desktop.EnsureValidLayout();
            Assert.That(desktop.CanPlace(first), Is.True);
            Assert.That(desktop.CanPlace(second), Is.True);
            Assert.Throws<System.ArgumentException>(() => Icon("first", new Vector2(400, -20)));
        }

        [Test]
        public void LosingFocusCancelsDragWithoutKeepingInvalidPosition()
        {
            var icon = Icon("memo", new Vector2(20, -20));
            icon.OnBeginDrag(Pointer(new Vector2(30, -30), new Vector2(100, -100)));
            typeof(DesktopController).GetMethod("OnApplicationFocus",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(desktop, new object[] { false });
            Assert.That(icon.Rect.anchoredPosition, Is.EqualTo(new Vector2(20, -20)));
        }
    }
}
