using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopIconView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesktopItemDefinition item;
        [SerializeField] private DesktopController desktop;
        [SerializeField] private Image selection;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameLabel;
        private bool suppressClick;

        public DesktopItemDefinition Item => item;
        public RectTransform Rect => (RectTransform)transform;
        public DesktopController Desktop => desktop;

        public void Configure(DesktopController owner, DesktopItemDefinition definition,
            Image highlight, Image image, Text label)
        {
            desktop = owner;
            item = definition;
            selection = highlight;
            iconImage = image;
            nameLabel = label;
            GameTypography.Apply(nameLabel);
            if (iconImage != null && item != null) iconImage.sprite = item.Icon;
            SetDisplayName(item != null ? item.DisplayName : "");
            SetSelected(false);
            if (owner != null) owner.Register(this);
        }

        public void SetDisplayName(string value)
        {
            if (nameLabel != null) nameLabel.text = value;
        }

        public void SetSelected(bool value)
        {
            if (selection != null)
                selection.color = value ? new Color(0.1f, 0.28f, 0.65f, 0.55f) : Color.clear;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!DesktopInputRouter.DesktopAllowed(data)) return;
            if (data.button != PointerEventData.InputButton.Left || desktop == null) return;
            suppressClick = false;
            desktop.PressIcon(this);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (!DesktopInputRouter.DesktopAllowed(data)) return;
            if (data.button != PointerEventData.InputButton.Left || suppressClick || desktop == null) return;
            desktop.Click(this, data.position, Time.unscaledTime);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (!DesktopInputRouter.DesktopAllowed(data)) return;
            if (data.button != PointerEventData.InputButton.Left || desktop == null) return;
            suppressClick = true;
            data.eligibleForClick = false;
            desktop.BeginDrag(this, data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!DesktopInputRouter.DesktopAllowed(data)) return;
            if (data.button == PointerEventData.InputButton.Left && desktop != null)
                desktop.Drag(this, data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && desktop != null)
            {
                desktop.Drag(this, data);
                desktop.EndDrag(this);
            }
        }

        private void OnDisable()
        {
            if (desktop != null) desktop.RemoveFromSelection(this);
        }
    }
}
