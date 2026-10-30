using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class TaskbarController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour sourceBehaviour;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private TaskbarButtonView buttonPrefab;
        [SerializeField] private Button scrollLeft;
        [SerializeField] private Button scrollRight;
        private readonly Dictionary<string, TaskbarButtonView> buttons = new Dictionary<string, TaskbarButtonView>();
        private readonly List<string> order = new List<string>();
        private ITaskbarWindowSource source;
        private Action sourceChanged;
        private int bindingVersion;
        private string highlighted;
        private Vector2 lastSize;
        private bool locked;
        public IReadOnlyDictionary<string, TaskbarButtonView> Buttons => buttons;
        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public bool Overflow { get; private set; }
        public string HighlightedWindowId => highlighted;

        public void Configure(MonoBehaviour provider, RectTransform view, RectTransform items,
            TaskbarButtonView prefab, Button left, Button right)
        {
            sourceBehaviour = provider;
            viewport = view;
            content = items;
            buttonPrefab = prefab;
            scrollLeft = left;
            scrollRight = right;
            left.onClick.RemoveListener(ScrollLeft);
            right.onClick.RemoveListener(ScrollRight);
            left.onClick.AddListener(ScrollLeft);
            right.onClick.AddListener(ScrollRight);
            Bind(provider as ITaskbarWindowSource);
        }

        private void OnEnable()
        {
            if (scrollLeft != null) scrollLeft.onClick.AddListener(ScrollLeft);
            if (scrollRight != null) scrollRight.onClick.AddListener(ScrollRight);
            Bind(sourceBehaviour != null ? sourceBehaviour as ITaskbarWindowSource : source);
        }
        private void OnDisable()
        {
            Unsubscribe();
            if (scrollLeft != null) scrollLeft.onClick.RemoveListener(ScrollLeft);
            if (scrollRight != null) scrollRight.onClick.RemoveListener(ScrollRight);
            ClearButtons();
        }

        public void Bind(ITaskbarWindowSource provider)
        {
            Unsubscribe();
            source = provider;
            sourceBehaviour = provider as MonoBehaviour;
            if (source == null || !isActiveAndEnabled) { ClearButtons(); return; }
            var version = bindingVersion;
            sourceChanged = () => { if (version == bindingVersion && isActiveAndEnabled) Refresh(); };
            source.Changed += sourceChanged;
            Refresh();
        }
        private void Unsubscribe()
        {
            bindingVersion++;
            if (source != null && sourceChanged != null) source.Changed -= sourceChanged;
            sourceChanged = null;
        }
        private void ClearButtons()
        {
            foreach (var button in buttons.Values) RemoveButton(button);
            buttons.Clear();
            order.Clear();
            highlighted = null;
            if (content != null) { content.sizeDelta = new Vector2(0, content.sizeDelta.y); content.anchoredPosition = Vector2.zero; }
            LayoutButtons();
        }
        private static void RemoveButton(TaskbarButtonView button)
        {
            if (button == null) return;
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject); else DestroyImmediate(button.gameObject);
        }

        public void Refresh()
        {
            if (source == null || content == null || buttonPrefab == null) return;
            var snapshot = source.GetSnapshot();
            locked = snapshot.IsLocked;
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null) scroll.enabled = !locked;
            var visible = new List<TaskbarWindowInfo>();
            var ids = new HashSet<string>();
            foreach (var info in snapshot.Windows)
                if (info != null && info.ShowInTaskbar && !string.IsNullOrEmpty(info.WindowId) && ids.Add(info.WindowId)) visible.Add(info);
            visible.Sort((a, b) => { var result = a.CreationOrder.CompareTo(b.CreationOrder); return result != 0 ? result : string.CompareOrdinal(a.WindowId, b.WindowId); });
            foreach (var id in new List<string>(buttons.Keys))
                if (!ids.Contains(id)) { RemoveButton(buttons[id]); buttons.Remove(id); }
            var nextHighlight = GetHighlight(snapshot);
            var changedActive = nextHighlight != highlighted;
            highlighted = nextHighlight;
            order.Clear();
            foreach (var info in visible)
            {
                if (!buttons.TryGetValue(info.WindowId, out var button))
                {
                    button = Instantiate(buttonPrefab, content);
                    button.gameObject.name = "Task_" + info.WindowId;
                    button.gameObject.SetActive(true);
                    buttons.Add(info.WindowId, button);
                }
                button.Bind(info, info.WindowId == highlighted, snapshot.IsLocked, ClickWindow);
                order.Add(info.WindowId);
                button.transform.SetAsLastSibling();
            }
            LayoutButtons();
            if (changedActive) RevealActive();
        }

        private static string GetHighlight(TaskbarWindowSnapshot snapshot)
        {
            var id = snapshot.ActiveWindowId;
            var visited = new HashSet<string>();
            while (id != null && visited.Add(id))
            {
                TaskbarWindowInfo found = null;
                foreach (var info in snapshot.Windows) if (info.WindowId == id) { found = info; break; }
                if (found == null) return null;
                if (found.ShowInTaskbar) return found.WindowId;
                id = found.OwnerWindowId;
            }
            return null;
        }

        public void ClickWindow(string id)
        {
            if (!isActiveAndEnabled || source == null) return;
            var snapshot = source.GetSnapshot();
            TaskbarWindowInfo info = null;
            foreach (var window in snapshot.Windows) if (window.WindowId == id) { info = window; break; }
            if (!snapshot.IsLocked && info != null && info.ShowInTaskbar)
            {
                if (info.IsMinimized) source.Request(id, TaskbarWindowAction.RestoreAndActivate);
                else if (snapshot.ActiveWindowId != id) source.Request(id, TaskbarWindowAction.Activate);
                else if (info.CanMinimize) source.Request(id, TaskbarWindowAction.Minimize);
            }
            // Re-read authoritative state even if a request was refused or the window vanished.
            Refresh();
        }

        private void LayoutButtons()
        {
            if (viewport == null || content == null || scrollLeft == null || scrollRight == null) return;
            var size = ((RectTransform)transform).rect.size;
            lastSize = size;
            var width = Mathf.Max(0, size.x - 16);
            var count = order.Count;
            var buttonWidth = count == 0 ? 144 : Mathf.Clamp((width - (count - 1) * 4) / count, 144, 240);
            Overflow = count > 0 && count * buttonWidth + (count - 1) * 4 > width;
            scrollLeft.gameObject.SetActive(Overflow);
            scrollRight.gameObject.SetActive(Overflow);
            viewport.offsetMin = new Vector2(Overflow ? 40 : 8, 6);
            viewport.offsetMax = new Vector2(Overflow ? -40 : -8, -6);
            viewport.ForceUpdateRectTransforms();
            content.sizeDelta = new Vector2(count == 0 ? 0 : count * buttonWidth + (count - 1) * 4, 0);
            for (var i = 0; i < count; i++)
            {
                var rect = (RectTransform)buttons[order[i]].transform;
                rect.anchoredPosition = new Vector2(i * (buttonWidth + 4), 0);
                rect.sizeDelta = new Vector2(buttonWidth, 0);
            }
            ClampScroll();
        }

        private void RevealActive()
        {
            if (highlighted == null || !buttons.TryGetValue(highlighted, out var button)) return;
            var rect = (RectTransform)button.transform;
            var left = rect.anchoredPosition.x + content.anchoredPosition.x;
            if (left < 0) MoveScroll(-left);
            else if (left + rect.rect.width > viewport.rect.width) MoveScroll(viewport.rect.width - left - rect.rect.width);
        }
        private void ScrollLeft() => MoveScroll(200);
        private void ScrollRight() => MoveScroll(-200);
        private void MoveScroll(float delta)
        {
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null) scroll.StopMovement();
            content.anchoredPosition += new Vector2(delta, 0);
            ClampScroll();
        }
        private void ClampScroll()
        {
            if (content == null || viewport == null) return;
            var min = Mathf.Min(0, viewport.rect.width - content.rect.width);
            content.anchoredPosition = new Vector2(Mathf.Clamp(content.anchoredPosition.x, min, 0), 0);
            scrollLeft.interactable = !locked && content.anchoredPosition.x < -0.5f;
            scrollRight.interactable = !locked && content.anchoredPosition.x > min + 0.5f;
        }
        private void LateUpdate()
        {
            if (viewport == null) return;
            if (((RectTransform)transform).rect.size != lastSize) { LayoutButtons(); RevealActive(); }
            ClampScroll();
        }
    }
}
