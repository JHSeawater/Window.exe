using UnityEngine;
using UnityEngine.EventSystems;

namespace WindowExe.Desktop
{
    public sealed class DesktopBackground : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesktopController desktop;
        private bool suppressClick;
        public void Configure(DesktopController owner) => desktop = owner;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) suppressClick = false;
        }

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && !suppressClick && desktop != null)
                desktop.ClearSelection();
        }

        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || desktop == null) return;
            suppressClick = true;
            data.eligibleForClick = false;
            desktop.BeginSelection(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && desktop != null) desktop.UpdateSelection(data);
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && desktop != null) desktop.EndSelection(data);
        }

        private void OnDisable()
        {
            if (desktop != null) desktop.CancelSelection();
        }
    }
}
