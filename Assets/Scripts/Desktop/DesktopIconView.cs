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
        public Text NameLabel => nameLabel;
        public Image IconImage => iconImage;

        public void SetIconSprite(Sprite sprite)
        {
            if (iconImage != null) iconImage.sprite = sprite;
        }

        public void Configure(DesktopController owner, DesktopItemDefinition definition,
            Image highlight, Image image, Text label)
        {
            desktop = owner;
            item = definition;
            selection = highlight;
            iconImage = image;
            nameLabel = label;
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
            if (data.button != PointerEventData.InputButton.Left || desktop == null) return;
            suppressClick = false;
            desktop.PressIcon(this);
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (suppressClick || desktop == null) return;

            KeyFileMechanic keyFile = GetComponent<KeyFileMechanic>();

            if (data.button == PointerEventData.InputButton.Right)
            {
                if (keyFile != null && keyFile.isActiveAndEnabled)
                    keyFile.ShowContextMenu(data.position);
                return;
            }

            if (data.button != PointerEventData.InputButton.Left) return;

            // key.txt는 일반 WindowManager 열기 요청 대신 전용 기믹으로 처리.
            if (keyFile != null && keyFile.isActiveAndEnabled)
            {
                keyFile.HandleLeftClick(data.position, Time.unscaledTime);
                return;
            }

            desktop.Click(this, data.position, Time.unscaledTime);
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || desktop == null) return;
            suppressClick = true;
            data.eligibleForClick = false;
            desktop.BeginDrag(this, data);
        }

        public void OnDrag(PointerEventData data)
        {
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
