using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace WindowExe.Desktop
{
    public sealed class DesktopController : MonoBehaviour
    {
        [SerializeField] private RectTransform iconArea;
        [SerializeField] private RectTransform windowLayer;
        [SerializeField] private DesktopSelectionBox selectionBox;
        [SerializeField] private List<DesktopIconView> icons = new List<DesktopIconView>();
        [SerializeField, Min(0.1f)] private float doubleClickSeconds = 0.3f;
        [SerializeField, Min(1)] private float doubleClickDistance = 12;
        [SerializeField] private UnityEvent<string> onOpenRequested = new UnityEvent<string>();
        private DesktopIconView selected;
        private readonly List<DesktopIconView> selectedIcons = new List<DesktopIconView>();
        private readonly List<DesktopIconView> selectionBeforeDrag = new List<DesktopIconView>();
        private DesktopIconView primaryBeforeDrag;
        private bool selecting;
        private Vector2 selectionStart;
        private Rect selectionBounds;
        private DesktopIconView lastClicked;
        private float lastClickTime;
        private Vector2 lastClickPosition;
        private DesktopIconView dragging;
        private readonly Dictionary<DesktopIconView, Vector3> dragOrigins = new Dictionary<DesktopIconView, Vector3>();
        private Rect dragBounds;
        private Vector2 dragPress;
        private bool layoutDirty;
        private Vector2 lastAreaSize;
        private Vector3 lastAreaScale;
        private Vector2Int lastScreenSize;

        public event Action<string> OpenRequested;
        public RectTransform IconArea => iconArea;
        public RectTransform WindowLayer => windowLayer;
        public DesktopIconView Selected => selected;
        public IReadOnlyList<DesktopIconView> SelectedIcons => selectedIcons;
        public bool IsSelecting => selecting;
        public bool IsDragging => dragging != null;
        public Rect SelectionBounds => selectionBounds;
        public IReadOnlyList<DesktopIconView> Icons => icons;
        public UnityEvent<string> OnOpenRequested => onOpenRequested;

        public void Configure(RectTransform area, RectTransform windows)
        {
            iconArea = area;
            windowLayer = windows;
            lastAreaSize = area != null ? area.rect.size : Vector2.zero;
            lastAreaScale = area != null ? area.lossyScale : Vector3.one;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        public void ConfigureSelectionBox(DesktopSelectionBox view) => selectionBox = view;

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
            ClearSelectedIcons();
            AddSelected(icon);
        }

        private bool IsAvailable(DesktopIconView icon) =>
            icon != null && icon.isActiveAndEnabled && icons.Contains(icon);

        private void ClearSelectedIcons()
        {
            foreach (var icon in selectedIcons)
                if (icon != null) icon.SetSelected(false);
            selectedIcons.Clear();
            selected = null;
        }

        private void AddSelected(DesktopIconView icon)
        {
            if (!IsAvailable(icon) || selectedIcons.Contains(icon)) return;
            selectedIcons.Add(icon);
            icon.SetSelected(true);
            if (selected == null) selected = icon;
        }

        public void PressIcon(DesktopIconView icon)
        {
            if (!isActiveAndEnabled || selecting || dragging != null || !IsAvailable(icon)) return;
            // Keep the group until release decides between a click and a drag.
            if (!selectedIcons.Contains(icon)) Select(icon);
            selected = icon;
        }

        public void ClearSelection()
        {
            if (!isActiveAndEnabled || selecting || dragging != null) return;
            Select(null);
            ResetClicks();
        }

        public void Click(DesktopIconView icon, Vector2 screenPosition, float time)
        {
            if (!isActiveAndEnabled || !IsAvailable(icon) || dragging != null || selecting) return;
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

        public void BeginSelection(PointerEventData data)
        {
            if (!isActiveAndEnabled || selecting || dragging != null || iconArea == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.pressPosition, data.pressEventCamera, out var start)) return;
            selectionBeforeDrag.Clear();
            selectionBeforeDrag.AddRange(selectedIcons);
            primaryBeforeDrag = selected;
            selectionStart = DesktopLayout.ClampPoint(start, iconArea.rect);
            selecting = true;
            ResetClicks();
            UpdateSelection(data);
        }

        public void UpdateSelection(PointerEventData data)
        {
            if (!selecting || iconArea == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.position, data.pressEventCamera, out var point)) return;
            selectionBounds = DesktopLayout.FromPoints(selectionStart, DesktopLayout.ClampPoint(point, iconArea.rect));
            ClearSelectedIcons();
            foreach (var icon in icons)
                if (IsAvailable(icon) && DesktopLayout.Intersects(selectionBounds,
                        DesktopLayout.BoundsInParent(icon.Rect, iconArea))) AddSelected(icon);
            if (selectionBox != null) selectionBox.Show(selectionBounds, iconArea);
        }

        public void EndSelection(PointerEventData data)
        {
            if (!selecting) return;
            UpdateSelection(data);
            selecting = false;
            selectionBeforeDrag.Clear();
            primaryBeforeDrag = null;
            if (selectionBox != null) selectionBox.Hide();
            ResetClicks();
        }

        public void CancelSelection()
        {
            if (!selecting) return;
            selecting = false;
            ClearSelectedIcons();
            foreach (var icon in selectionBeforeDrag) AddSelected(icon);
            if (selectedIcons.Contains(primaryBeforeDrag)) selected = primaryBeforeDrag;
            selectionBeforeDrag.Clear();
            primaryBeforeDrag = null;
            if (selectionBox != null) selectionBox.Hide();
            ResetClicks();
        }

        public void BeginDrag(DesktopIconView icon, PointerEventData data)
        {
            if (!isActiveAndEnabled || selecting || dragging != null || iconArea == null || !IsAvailable(icon)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.pressPosition, data.pressEventCamera, out dragPress)) return;
            if (!selectedIcons.Contains(icon)) Select(icon);
            selected = icon;
            ResetClicks();
            dragOrigins.Clear();
            dragBounds = DesktopLayout.BoundsInParent(icon.Rect, iconArea);
            foreach (var member in selectedIcons)
            {
                if (!IsAvailable(member)) continue;
                dragOrigins.Add(member, member.Rect.localPosition);
                dragBounds = DesktopLayout.Union(dragBounds, DesktopLayout.BoundsInParent(member.Rect, iconArea));
            }
            if (dragBounds.width > iconArea.rect.width || dragBounds.height > iconArea.rect.height)
            {
                dragOrigins.Clear();
                return;
            }
            dragging = icon;
            foreach (var member in selectedIcons) member.transform.SetAsLastSibling();
            Drag(icon, data);
        }

        public void Drag(DesktopIconView icon, PointerEventData data)
        {
            if (dragging != icon || iconArea == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(iconArea,
                    data.position, data.pressEventCamera, out var point)) return;
            var movement = point - dragPress;
            var candidate = new Rect(dragBounds.position + movement, dragBounds.size);
            movement += DesktopLayout.ClampOffset(candidate, iconArea.rect);
            foreach (var entry in dragOrigins)
                if (entry.Key != null) entry.Key.Rect.localPosition = entry.Value + new Vector3(movement.x, movement.y, 0);
        }

        public bool EndDrag(DesktopIconView icon)
        {
            if (dragging != icon) return false;
            var accepted = true;
            foreach (var member in dragOrigins.Keys)
                if (!IsAvailable(member) || !CanPlace(member)) { accepted = false; break; }
            if (!accepted) RestoreDragOrigins();
            dragging = null;
            dragOrigins.Clear();
            ResetClicks();
            return accepted;
        }

        public void CancelDrag(DesktopIconView icon)
        {
            if (dragging == null || (dragging != icon && !dragOrigins.ContainsKey(icon))) return;
            RestoreDragOrigins();
            dragging = null;
            dragOrigins.Clear();
            ResetClicks();
        }

        private void RestoreDragOrigins()
        {
            foreach (var entry in dragOrigins)
                if (entry.Key != null) entry.Key.Rect.localPosition = entry.Value;
        }

        public void RemoveFromSelection(DesktopIconView icon)
        {
            CancelDrag(icon);
            if (icon != null) icon.SetSelected(false);
            selectedIcons.Remove(icon);
            selectionBeforeDrag.Remove(icon);
            if (selected == icon) selected = selectedIcons.Count > 0 ? selectedIcons[0] : null;
        }

        public void CancelInteraction()
        {
            CancelSelection();
            if (dragging != null) CancelDrag(dragging);
            ResetClicks();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CancelInteraction();
        }

        private void OnDisable() => CancelInteraction();

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelInteraction();
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
            if (iconArea == null || selecting || dragging != null) return;
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
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (iconArea != null && (lastAreaSize != iconArea.rect.size || lastAreaScale != iconArea.lossyScale
                    || lastScreenSize != screenSize))
            {
                CancelInteraction();
                lastAreaSize = iconArea.rect.size;
                lastAreaScale = iconArea.lossyScale;
                lastScreenSize = screenSize;
                layoutDirty = true;
            }
            if (!layoutDirty || dragging != null || selecting) return;
            layoutDirty = false;
            EnsureValidLayout();
        }
    }
}
