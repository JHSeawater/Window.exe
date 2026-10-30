using UnityEngine;
using UnityEngine.EventSystems;

namespace WindowExe.Desktop
{
    public sealed class DesktopBackground : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private DesktopController desktop;
        public void Configure(DesktopController owner) => desktop = owner;
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && desktop != null)
                desktop.ClearSelection();
        }
    }
}
