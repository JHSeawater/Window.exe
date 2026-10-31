using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public static class TaskbarUiFactory
    {
        public static TaskbarButtonView CreateButtonTemplate(Transform parent)
        {
            var rect = Rect("TaskbarButton", parent);
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.sizeDelta = new Vector2(200, 0);
            var background = rect.gameObject.AddComponent<Image>();
            var glyph = Rect("Icon", rect);
            glyph.anchorMin = glyph.anchorMax = new Vector2(0, 0.5f);
            glyph.pivot = new Vector2(0, 0.5f);
            glyph.anchoredPosition = new Vector2(8, 0);
            glyph.sizeDelta = new Vector2(24, 24);
            var image = glyph.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var label = Label("Title", rect, "", 18);
            Stretch(label.rectTransform, new Vector2(40, 0), new Vector2(-8, 0));
            label.alignment = TextAnchor.MiddleLeft;
            var view = rect.gameObject.AddComponent<TaskbarButtonView>();
            view.Configure(background, image, label);
            return view;
        }

        public static TaskbarController Create(DesktopController desktop, MonoBehaviour source, TaskbarButtonView prefab)
        {
            var bar = Rect("Taskbar", desktop.transform);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, 48);
            var backdrop = bar.gameObject.AddComponent<Image>();
            var viewport = Rect("Viewport", bar);
            Stretch(viewport, new Vector2(8, 6), new Vector2(-8, -6));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Windows", viewport);
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.sizeDelta = Vector2.zero;
            var scroll = bar.gameObject.AddComponent<TaskbarScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
            var left = Arrow("ScrollLeft", bar, "‹", false);
            var right = Arrow("ScrollRight", bar, "›", true);
            var controller = bar.gameObject.AddComponent<TaskbarController>();
            controller.Configure(source, viewport, content, prefab, left, right);
            ApplyAppearance(controller);
            var workArea = desktop.GetComponent<DesktopWorkArea>();
            if (workArea == null) workArea = desktop.gameObject.AddComponent<DesktopWorkArea>();
            workArea.Configure(desktop, bar);
            bar.SetAsLastSibling();
            return controller;
        }

        public static void ApplyAppearance(TaskbarController controller)
        {
            var bar = (RectTransform)controller.transform;
            bar.GetComponent<Image>().color = new Color(0.24f, 0.30f, 0.39f, 1);
            var separator = bar.Find("TopBorder") as RectTransform;
            if (separator == null) separator = Rect("TopBorder", bar);
            separator.anchorMin = new Vector2(0, 1);
            separator.anchorMax = Vector2.one;
            separator.pivot = new Vector2(0.5f, 1);
            separator.anchoredPosition = Vector2.zero;
            separator.sizeDelta = new Vector2(0, 2);
            var image = separator.GetComponent<Image>();
            if (image == null) image = separator.gameObject.AddComponent<Image>();
            image.color = new Color(0.53f, 0.62f, 0.73f, 1);
            image.raycastTarget = false;
        }

        private static Button Arrow(string name, RectTransform bar, string text, bool right)
        {
            var rect = Rect(name, bar);
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1 : 0, 0.5f);
            rect.pivot = new Vector2(right ? 1 : 0, 0.5f);
            rect.anchoredPosition = new Vector2(right ? -4 : 4, 0);
            rect.sizeDelta = new Vector2(32, 36);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.25f);
            var label = Label("Label", rect, text, 24);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            var button = rect.gameObject.AddComponent<TaskbarInputButton>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }
        private static Text Label(string name, Transform parent, string text, int fontSize)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            GameTypography.Apply(label);
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            return rect;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
