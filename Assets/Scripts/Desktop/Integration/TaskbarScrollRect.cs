using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace WindowExe.Desktop
{
    public sealed class TaskbarScrollRect : ScrollRect
    {
        public override void OnScroll(PointerEventData e) { if(DesktopInputRouter.WheelAllowed(e.position)) base.OnScroll(e); }
        public override void OnInitializePotentialDrag(PointerEventData e) { if(DesktopInputRouter.TaskbarAllowed(e)) base.OnInitializePotentialDrag(e); }
        public override void OnBeginDrag(PointerEventData e) { if(DesktopInputRouter.TaskbarAllowed(e)) base.OnBeginDrag(e); }
        public override void OnDrag(PointerEventData e) { if(DesktopInputRouter.TaskbarAllowed(e)) base.OnDrag(e); }
        public override void OnEndDrag(PointerEventData e) { base.OnEndDrag(e); }
    }
}
