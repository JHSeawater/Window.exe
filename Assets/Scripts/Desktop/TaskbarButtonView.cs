using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class TaskbarButtonView : Button, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image glyph;
        [SerializeField] private Text titleLabel;
        private string fullTitle = "";
        private bool suppressClick;
        private string fittedTitle;
        private Font fittedFont;
        private int fittedSize;
        private float fittedWidth = -1;
        private float fittedUnits;
        private Action<string> clicked;
        public string WindowId { get; private set; }
        public string VisibleTitle => titleLabel != null ? titleLabel.text : "";
        public bool Active { get; private set; }

        public void Configure(Image background, Image image, Text label)
        {
            targetGraphic = background;
            glyph = image;
            titleLabel = label;
            navigation = new Navigation { mode = Navigation.Mode.None };
            onClick.RemoveListener(Dispatch);
            onClick.AddListener(Dispatch);
        }

        protected override void Awake()
        {
            base.Awake();
            onClick.RemoveListener(Dispatch);
            onClick.AddListener(Dispatch);
        }

        private void Dispatch() => clicked?.Invoke(WindowId);
        public void Bind(TaskbarWindowInfo info, bool active, bool locked, Action<string> callback)
        {
            WindowId = info.WindowId;
            clicked = callback;
            fullTitle = info.Title;
            Active = active;
            interactable = !locked;
            var colors = colorsFor(active);
            this.colors = colors;
            if (glyph != null)
            {
                glyph.sprite = info.Icon;
                glyph.color = info.Icon != null ? Color.white : new Color(0.55f, 0.75f, 0.95f);
            }
            GameTypography.Apply(titleLabel);
            FitTitle();
        }

        private static ColorBlock colorsFor(bool active)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = active ? new Color(0.19f, 0.36f, 0.6f) : new Color(0.14f, 0.18f, 0.25f);
            colors.highlightedColor = new Color(0.26f, 0.4f, 0.58f);
            colors.pressedColor = new Color(0.1f, 0.25f, 0.45f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.1f, 0.12f, 0.16f);
            colors.fadeDuration = 0.05f;
            return colors;
        }

        private void LateUpdate() => FitTitle();
        private void FitTitle()
        {
            if (titleLabel == null || titleLabel.font == null) return;
            var width = titleLabel.rectTransform.rect.width;
            var units = titleLabel.pixelsPerUnit;
            if (fittedTitle == fullTitle && fittedFont == titleLabel.font && fittedSize == titleLabel.fontSize
                && Mathf.Approximately(fittedWidth, width) && Mathf.Approximately(fittedUnits, units)) return;
            fittedTitle = fullTitle;
            fittedFont = titleLabel.font;
            fittedSize = titleLabel.fontSize;
            fittedWidth = width;
            fittedUnits = units;
            var settings = titleLabel.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            var generator = titleLabel.cachedTextGeneratorForLayout;
            if (generator.GetPreferredWidth(fullTitle, settings) / units <= width)
            {
                titleLabel.text = fullTitle;
                return;
            }
            var low = 0;
            var high = fullTitle.Length;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (generator.GetPreferredWidth(fullTitle.Substring(0, mid) + "…", settings) / units <= width) low = mid;
                else high = mid - 1;
            }
            if (low > 0 && char.IsHighSurrogate(fullTitle[low - 1])) low--;
            titleLabel.text = fullTitle.Substring(0, low) + "…";
        }

        public override void OnPointerDown(PointerEventData data)
        {
            if (!DesktopInputRouter.TaskbarAllowed(data)) return;
            if (data.button == PointerEventData.InputButton.Left) suppressClick = false;
            base.OnPointerDown(data);
        }
        public override void OnPointerClick(PointerEventData data)
        {
            if (!DesktopInputRouter.TaskbarAllowed(data)) return;
            if (!suppressClick) base.OnPointerClick(data);
        }
        public void OnInitializePotentialDrag(PointerEventData data)
        {
            data.useDragThreshold = true;
            GetComponentInParent<ScrollRect>()?.OnInitializePotentialDrag(data);
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (!DesktopInputRouter.TaskbarAllowed(data)) return;
            suppressClick = true;
            data.eligibleForClick = false;
            GetComponentInParent<ScrollRect>()?.OnBeginDrag(data);
        }
        public void OnDrag(PointerEventData data) => GetComponentInParent<ScrollRect>()?.OnDrag(data);
        public void OnEndDrag(PointerEventData data) => GetComponentInParent<ScrollRect>()?.OnEndDrag(data);
    }
}
