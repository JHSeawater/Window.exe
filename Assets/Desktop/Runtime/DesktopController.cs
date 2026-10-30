using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace WindowExe.Desktop
{
    public sealed class DesktopController : MonoBehaviour
    {
        [SerializeField] private RectTransform iconArea;
        [SerializeField] private RectTransform windowLayer;
        [SerializeField] private List<DesktopIconView> icons = new List<DesktopIconView>();
        [SerializeField, Min(0.1f)] private float doubleClickSeconds = 0.3f;
        [SerializeField, Min(1)] private float doubleClickDistance = 12;
        [SerializeField] private UnityEvent<string> onOpenRequested = new UnityEvent<string>();
        private DesktopIconView selected;
        private DesktopIconView lastClicked;
        private float lastClickTime;
        private Vector2 lastClickPosition;
        private DesktopIconView dragging;
        private Vector3 dragOrigin;
        private Vector2 dragPress;
        private bool layoutDirty;

        public event Action<string> OpenRequested;
        public RectTransform IconArea => iconArea;
        public RectTransform WindowLayer => windowLayer;
        public DesktopIconView Selected => selected;
        public IReadOnlyList<DesktopIconView> Icons => icons;
        public UnityEvent<string> OnOpenRequested => onOpenRequested;

        public void Configure(RectTransform area, RectTransform windows)
        {
            iconArea = area;
            windowLayer = windows;
        }

        public void Register(DesktopIconView icon)
        {
            if (icon == null || icon.Item == null || icons.Contains(icon)) return;
            if (string.IsNullOrWhiteSpace(icon.Item.ItemId))
                throw new ArgumentException("Desktop item needs a stable ID.");
            if (icons.Exists(other => other != null && other.Item.ItemId == icon.Item.ItemId))
                throw new ArgumentException("Duplicate desktop item ID: " + icon.Item.ItemId);
            icons.Add(icon);
        }

        private void Start()
        {
            Canvas.ForceUpdateCanvases();
            EnsureValidLayout();
        }

        public void Select(DesktopIconView icon)
        {
            if (selected != null) selected.SetSelected(false);
            selected = icon;
            if (selected != null) selected.SetSelected(true);
        }

        public void ClearSelection()
        {
            Select(null);
            ResetClicks();
        }

        public void Click(DesktopIconView icon, Vector2 screenPosition, float time)
        {
            if (icon == null || !icons.Contains(icon) || dragging != null) return;
            Select(icon);
            if (lastClicked == icon && time >= lastClickTime && time - lastClickTime <= doubleClickSeconds
                && Vector2.Distance(screenPosition, lastClickPosition) <= doubleClickDistance)
            {
                ResetClicks();
                OpenRequested?.Invoke(icon.Item.ItemId);
                onOpenRequested.Invoke(icon.Item.ItemId);
                return;
            }
            lastClicked = icon;
            lastClickTime = time;
            lastClickPosition = screenPosition;
        }

        private void ResetClicks() => lastClicked = null;

        public void BeginDrag(DesktopIconView icon, PointerEventData data)
        {
            if (dragging != null || iconArea == null || !icons.Contains(icon)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.pressPosition, data.pressEventCamera, out dragPress)) return;
            dragging = icon;
            dragOrigin = icon.Rect.localPosition;
            Select(icon);
            ResetClicks();
            icon.transform.SetAsLastSibling();
            Drag(icon, data);
        }

        public void Drag(DesktopIconView icon, PointerEventData data)
        {
            if (dragging != icon || iconArea == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.position, data.pressEventCamera, out var point)) return;
            var movement = point - dragPress;
            icon.Rect.localPosition = dragOrigin + new Vector3(movement.x, movement.y, 0);
            Clamp(icon);
        }

        public bool EndDrag(DesktopIconView icon)
        {
            if (dragging != icon) return false;
            Clamp(icon);
            var accepted = CanPlace(icon);
            if (!accepted) icon.Rect.localPosition = dragOrigin;
            dragging = null;
            ResetClicks();
            return accepted;
        }

        public void CancelDrag(DesktopIconView icon)
        {
            if (dragging != icon) return;
            icon.Rect.localPosition = dragOrigin;
            dragging = null;
            ResetClicks();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (dragging != null) CancelDrag(dragging);
                ResetClicks();
            }
        }

        private void Clamp(DesktopIconView icon)
        {
            var offset = DesktopLayout.ClampOffset(DesktopLayout.BoundsInParent(icon.Rect, iconArea), iconArea.rect);
            icon.Rect.localPosition += new Vector3(offset.x, offset.y, 0);
        }

        public bool CanPlace(DesktopIconView icon)
        {
            if (iconArea == null) return false;
            var bounds = DesktopLayout.BoundsInParent(icon.Rect, iconArea);
            if (!DesktopLayout.Fits(bounds, iconArea.rect)) return false;
            foreach (var other in icons)
            {
                if (other == null || other == icon || !other.gameObject.activeInHierarchy) continue;
                if (bounds.Overlaps(DesktopLayout.BoundsInParent(other.Rect, iconArea))) return false;
            }
            return true;
        }

        public void EnsureValidLayout()
        {
            if (iconArea == null) return;
            foreach (var icon in icons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                Clamp(icon);
                if (CanPlace(icon)) continue;
                var original = icon.Rect.localPosition;
                var size = icon.Rect.rect.size;
                var found = false;
                for (var y = iconArea.rect.yMax - size.y; y >= iconArea.rect.yMin && !found; y -= size.y + 8)
                    for (var x = iconArea.rect.xMin; x <= iconArea.rect.xMax - size.x && !found; x += size.x + 8)
                    {
                        icon.Rect.localPosition = new Vector3(x + size.x * icon.Rect.pivot.x,
                            y + size.y * icon.Rect.pivot.y, original.z);
                        found = CanPlace(icon);
                    }
                if (!found)
                {
                    icon.Rect.localPosition = original;
                    Debug.LogWarning("Desktop has no free space for " + icon.Item.ItemId, icon);
                }
            }
        }

        private void OnRectTransformDimensionsChange() => layoutDirty = true;
        private void LateUpdate()
        {
            if (!layoutDirty || dragging != null) return;
            layoutDirty = false;
            EnsureValidLayout();
        }
    }
}
