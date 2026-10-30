using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class DesktopSelectionBox : MonoBehaviour
    {
        [SerializeField] private RectTransform box;
        public bool Visible => box != null && box.gameObject.activeSelf;

        public void Show(Rect bounds, RectTransform area)
        {
            if (box == null || area == null) return;
            var parent = (RectTransform)box.parent;
            Vector2 min = parent.InverseTransformPoint(area.TransformPoint(bounds.min));
            Vector2 max = parent.InverseTransformPoint(area.TransformPoint(bounds.max));
            box.localPosition = new Vector3(min.x, min.y, 0);
            box.sizeDelta = max - min;
            box.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (box != null) box.gameObject.SetActive(false);
        }

        public static DesktopSelectionBox Create(RectTransform area)
        {
            var layer = new GameObject("SelectionLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.gameObject.layer = 5;
            layer.SetParent(area.parent, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetSiblingIndex(area.GetSiblingIndex() + 1);
            var view = layer.gameObject.AddComponent<DesktopSelectionBox>();
            var fill = new GameObject("SelectionBox", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fill.gameObject.layer = 5;
            view.box = fill.rectTransform;
            view.box.SetParent(layer, false);
            view.box.anchorMin = view.box.anchorMax = new Vector2(0.5f, 0.5f);
            view.box.pivot = Vector2.zero;
            fill.color = new Color(0.15f, 0.45f, 0.95f, 0.18f);
            fill.raycastTarget = false;
            Border(view.box, "Top", new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), new Vector2(0, 1.5f));
            Border(view.box, "Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 1.5f));
            Border(view.box, "Left", Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(1.5f, 0));
            Border(view.box, "Right", new Vector2(1, 0), Vector2.one, new Vector2(1, 0.5f), new Vector2(1.5f, 0));
            view.Hide();
            return view;
        }

        private static void Border(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = 5;
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            image.color = new Color(0.25f, 0.6f, 1, 0.9f);
            image.raycastTarget = false;
        }
    }
}
