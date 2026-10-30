using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WindowExe.Desktop.Tests
{
    public sealed class TaskbarTests
    {
        [Test]
        public void SameItemRestoresSameInstanceAndReopenUsesNewId()
        {
            var model = new TaskbarDemoModel();
            var id = model.Open("memo", "메모");
            model.SetMaximized(id, true);
            Assert.That(model.Request(id, TaskbarWindowAction.Minimize), Is.True);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.Null);
            Assert.That(model.Open("memo", "메모"), Is.EqualTo(id));
            Assert.That(model.Find(id).IsMinimized, Is.False);
            Assert.That(model.Find(id).IsMaximized, Is.True);
            model.Close(id);
            Assert.That(model.Request(id, TaskbarWindowAction.Activate), Is.False);
            var reopened = model.Open("memo", "메모");
            Assert.That(reopened, Is.Not.EqualTo(id));
            model.Reset();
            var reset = model.Open("memo", "메모");
            Assert.That(reset, Is.Not.EqualTo(reopened));
            Assert.That(model.Request(reopened, TaskbarWindowAction.Minimize), Is.False);
        }

        [Test]
        public void MinimizeAndCloseSelectMostRecentEligibleWindow()
        {
            var model = new TaskbarDemoModel();
            var first = model.Open("first", "First");
            var second = model.Open("second", "Second");
            var third = model.Open("third", "Third");
            model.Request(first, TaskbarWindowAction.Activate);
            model.Request(first, TaskbarWindowAction.Minimize);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.EqualTo(third));
            model.Close(third);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.EqualTo(second));
            model.Request(second, TaskbarWindowAction.Minimize);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.Null);
        }

        [Test]
        public void ModalOwnerActivationRedirectsButMinimizeAndLockedRequestsAreRejected()
        {
            var model = new TaskbarDemoModel();
            var other = model.Open("other", "Other");
            var owner = model.Open("owner", "Owner");
            var modal = model.Open(null, "Warning", show: false, canMinimize: false, ownerId: owner, modal: true);
            Assert.That(model.Request(other, TaskbarWindowAction.Activate), Is.False);
            Assert.That(model.Request(owner, TaskbarWindowAction.Minimize), Is.False);
            Assert.That(model.Request(owner, TaskbarWindowAction.Activate), Is.True);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.EqualTo(modal));
            model.SetLocked(true);
            Assert.That(model.Request(owner, TaskbarWindowAction.Activate), Is.False);
            model.SetLocked(false);
            model.Close(owner);
            Assert.That(model.Find(modal), Is.Null);
            Assert.That(model.GetSnapshot().ActiveWindowId, Is.EqualTo(other));
        }

        [Test]
        public void SnapshotsAreStableAndNonMinimizableWindowStaysVisible()
        {
            var model = new TaskbarDemoModel();
            var id = model.Open("fixed", "Fixed", canMinimize: false);
            var previous = model.GetSnapshot();
            Assert.That(model.Request(id, TaskbarWindowAction.Minimize), Is.False);
            model.SetAppearance(id, "Changed", null, true, true);
            model.Request(id, TaskbarWindowAction.Minimize);
            Assert.That(previous.Windows[0].Title, Is.EqualTo("Fixed"));
            Assert.That(previous.Windows[0].IsMinimized, Is.False);
            Assert.That(model.Find(id).IsMinimized, Is.True);
        }

        private GameObject root;
        private DesktopController desktop;
        private TaskbarDemoSource demo;
        private TaskbarController bar;
        private readonly List<DesktopItemDefinition> definitions = new List<DesktopItemDefinition>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TaskbarTest", typeof(RectTransform));
            var canvas = (RectTransform)root.transform;
            canvas.pivot = new Vector2(0, 1);
            canvas.sizeDelta = new Vector2(800, 600);
            desktop = root.AddComponent<DesktopController>();
            var area = new GameObject("Icons", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(canvas, false);
            area.pivot = new Vector2(0, 1);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(20, 20);
            area.offsetMax = new Vector2(-20, -20);
            desktop.Configure(area, null);
            demo = root.AddComponent<TaskbarDemoSource>();
            demo.Configure(desktop);
            var template = TaskbarUiFactory.CreateButtonTemplate(canvas);
            template.gameObject.SetActive(false);
            bar = TaskbarUiFactory.Create(desktop, demo, template);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (var definition in definitions) Object.DestroyImmediate(definition);
            definitions.Clear();
        }

        [Test]
        public void ExistingWindowsMetadataAndRemovedWindowsSynchronizeWithoutDuplicates()
        {
            bar.enabled = false;
            var id = demo.Model.Open("memo", "Before enable");
            bar.enabled = true;
            // EditMode does not run MonoBehaviour lifecycle callbacks; Bind is explicit here.
            bar.Bind(demo);
            Assert.That(bar.Buttons.Count, Is.EqualTo(1));
            Assert.That(bar.Buttons.ContainsKey(id), Is.True);
            demo.Model.SetAppearance(id, "Changed", null, false, true);
            Assert.That(bar.Buttons, Is.Empty);
            Assert.That(demo.Model.Find(id).IsMinimized, Is.False, "Button exclusion is not window hiding.");
            demo.Model.SetAppearance(id, "Changed", null, true, true);
            Assert.That(bar.Buttons.Count, Is.EqualTo(1));
            demo.Model.Open("memo", "Again");
            Assert.That(bar.Buttons.Count, Is.EqualTo(1));
            demo.Model.Close(id);
            Assert.That(bar.Buttons, Is.Empty);
        }

        [Test]
        public void ButtonUsesActualActiveIdRatherThanModalOwnerHighlight()
        {
            var owner = demo.Model.Open("owner", "Owner");
            var modal = demo.Model.Open(null, "Warning", show: false, canMinimize: false, ownerId: owner, modal: true);
            Assert.That(bar.HighlightedWindowId, Is.EqualTo(owner));
            bar.ClickWindow(owner);
            Assert.That(demo.Model.GetSnapshot().ActiveWindowId, Is.EqualTo(modal));
            Assert.That(demo.Model.Find(owner).IsMinimized, Is.False);
            demo.Model.SetLocked(true);
            bar.ClickWindow(owner);
            Assert.That(bar.Buttons[owner].interactable, Is.False);
            Assert.That(bar.GetComponent<ScrollRect>().enabled, Is.False);
        }

        [Test]
        public void ButtonsToggleOneInstanceAndCreationOrderDoesNotFollowActivation()
        {
            var first = demo.Model.Open("first", "First");
            var second = demo.Model.Open("second", "Second");
            bar.ClickWindow(first);
            Assert.That(demo.Model.GetSnapshot().ActiveWindowId, Is.EqualTo(first));
            Assert.That(bar.Buttons[first].transform.GetSiblingIndex(), Is.LessThan(bar.Buttons[second].transform.GetSiblingIndex()));
            bar.ClickWindow(first);
            Assert.That(demo.Model.Find(first).IsMinimized, Is.True);
            bar.ClickWindow(first);
            Assert.That(demo.Model.Find(first).IsMinimized, Is.False);
            Assert.That(demo.Model.GetSnapshot().Windows.Count, Is.EqualTo(2));
        }

        [Test]
        public void RebindDoesNotLeakSubscriptionsOrAcceptOldSourceCallbacks()
        {
            var first = new TrackingSource("first");
            var second = new TrackingSource("second");
            bar.Bind(first);
            var stale = first.Handlers;
            bar.Bind(first);
            Assert.That(first.SubscriberCount, Is.EqualTo(1));
            bar.Bind(second);
            Assert.That(first.SubscriberCount, Is.Zero);
            stale?.Invoke();
            Assert.That(bar.Buttons.ContainsKey("second"), Is.True);
            Assert.That(bar.Buttons.ContainsKey("first"), Is.False);
            bar.Bind(null);
            Assert.That(second.SubscriberCount, Is.Zero);
            Assert.That(bar.Buttons, Is.Empty);
            Assert.That(bar.Overflow, Is.False);
        }

        [Test]
        public void OverflowKeepsAllButtonsReachableAndTitlesEllipsized()
        {
            string last = null;
            for (var i = 0; i < 20; i++) last = demo.Model.Open("item" + i, new string('W', 100));
            Assert.That(bar.Overflow, Is.True);
            Assert.That(bar.Buttons.Count, Is.EqualTo(20));
            Assert.That(bar.Content.anchoredPosition.x, Is.LessThan(0));
            var button = bar.Buttons[last];
            Assert.That(button.VisibleTitle.EndsWith("…"), Is.True);
            var rect = (RectTransform)button.transform;
            var right = rect.anchoredPosition.x + rect.rect.width + bar.Content.anchoredPosition.x;
            Assert.That(right, Is.LessThanOrEqualTo(bar.Viewport.rect.width + 0.1f));
            demo.Model.Reset();
            Assert.That(bar.Buttons, Is.Empty);
            Assert.That(bar.Overflow, Is.False);
        }

        [Test]
        public void WorkAreaCancelsMovementPreservesValidIconsAndReclampsOnlyOutOfBoundsIcons()
        {
            var first = Icon("first", new Vector2(20, -20));
            var bottom = Icon("bottom", new Vector2(20, -450));
            var original = first.Rect.anchoredPosition;
            var data = new PointerEventData(null) { pressPosition = new Vector2(50, -50), position = new Vector2(150, -150) };
            desktop.BeginDrag(first, data);
            var area = root.GetComponent<DesktopWorkArea>();
            area.SetBarHeight(160);
            Assert.That(desktop.IsDragging, Is.False);
            Assert.That(first.Rect.anchoredPosition, Is.EqualTo(original));
            Assert.That(desktop.CanPlace(first), Is.True);
            Assert.That(desktop.CanPlace(bottom), Is.True);
            Assert.That(area.Area.height, Is.EqualTo(440));
            Assert.That(desktop.IconArea.rect.height, Is.EqualTo(400));
            Assert.That(area.BoundsIn((RectTransform)root.transform), Is.EqualTo(area.Area));
        }

        private DesktopIconView Icon(string id, Vector2 position)
        {
            var item = ScriptableObject.CreateInstance<DesktopItemDefinition>();
            definitions.Add(item);
            item.Configure(id, id, null);
            var rect = new GameObject(id, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(desktop.IconArea, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(120, 140);
            rect.anchoredPosition = position;
            var icon = rect.gameObject.AddComponent<DesktopIconView>();
            icon.Configure(desktop, item, rect.GetComponent<Image>(), null, null);
            return icon;
        }

        private sealed class TrackingSource : ITaskbarWindowSource
        {
            public Action Handlers;
            public int SubscriberCount => Handlers?.GetInvocationList().Length ?? 0;
            private readonly string id;
            public TrackingSource(string name) { id = name; }
            public event Action Changed { add => Handlers += value; remove => Handlers -= value; }
            public TaskbarWindowSnapshot GetSnapshot() => new TaskbarWindowSnapshot(new[] { new TaskbarWindowInfo(id, id, id) }, id);
            public bool Request(string windowId, TaskbarWindowAction action) => false;
        }
    }
}
