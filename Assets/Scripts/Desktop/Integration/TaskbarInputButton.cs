using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace WindowExe.Desktop
{
    public sealed class TaskbarInputButton : Button
    {
        public override void OnPointerDown(PointerEventData e) { if(DesktopInputRouter.TaskbarAllowed(e)) base.OnPointerDown(e); }
        public override void OnPointerClick(PointerEventData e) { if(DesktopInputRouter.TaskbarAllowed(e)) base.OnPointerClick(e); }
    }
}
