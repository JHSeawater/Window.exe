using System;
using UnityEngine;

namespace WindowExe.Desktop
{
    [DefaultExecutionOrder(-100)]
    public sealed class DesktopWorkArea : MonoBehaviour
    {
        [SerializeField] private DesktopController desktop;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform taskbar;
        [SerializeField, Min(0)] private float barHeight = 48;
        [SerializeField, Min(0)] private float iconInset = 20;
        private Rect lastArea;
        public Rect Area { get; private set; }
        public float BarHeight => barHeight;
        public event Action<Rect> Changed;

        public void Configure(DesktopController owner, RectTransform bar)
        {
            desktop = owner;
            canvasRect = (RectTransform)owner.transform;
            taskbar = bar;
            Apply();
        }

        public void SetBarHeight(float height) { barHeight = Mathf.Max(0, height); Apply(); }
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (desktop == null || canvasRect == null || desktop.IconArea == null) return;
            var height = Mathf.Min(barHeight, canvasRect.rect.height);
            Area = Rect.MinMaxRect(canvasRect.rect.xMin, canvasRect.rect.yMin + height,
                canvasRect.rect.xMax, canvasRect.rect.yMax);
            var area = desktop.IconArea;
            var min = new Vector2(iconInset, height + iconInset);
            var max = new Vector2(-iconInset, -iconInset);
            if (area.anchorMin != Vector2.zero || area.anchorMax != Vector2.one || area.offsetMin != min || area.offsetMax != max)
            {
                desktop.CancelInteraction();
                area.anchorMin = Vector2.zero;
                area.anchorMax = Vector2.one;
                area.offsetMin = min;
                area.offsetMax = max;
                area.ForceUpdateRectTransforms();
                desktop.EnsureValidLayout();
            }
            if (taskbar != null) taskbar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (Area != lastArea) { lastArea = Area; Changed?.Invoke(Area); }
        }

        public Rect BoundsIn(RectTransform parent)
        {
            var min = parent.InverseTransformPoint(canvasRect.TransformPoint(Area.min));
            var max = parent.InverseTransformPoint(canvasRect.TransformPoint(Area.max));
            return DesktopLayout.FromPoints(min, max);
        }
    }
}
