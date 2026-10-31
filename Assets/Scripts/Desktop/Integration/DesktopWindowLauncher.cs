using UnityEngine;
using WindowExe.Desktop.Desktop.Window;
namespace WindowExe.Desktop
{
    public sealed class DesktopWindowLauncher : MonoBehaviour
    {
        [SerializeField] private DesktopController desktop;
        [SerializeField] private WindowManager manager;
        public void Configure(DesktopController surface, WindowManager windows)
        { if(desktop!=null) desktop.OpenRequested-=Open; desktop=surface; manager=windows; if(isActiveAndEnabled&&desktop!=null) desktop.OpenRequested+=Open; }
        private void OnEnable() { if(desktop!=null) desktop.OpenRequested+=Open; }
        private void OnDisable() { if(desktop!=null) desktop.OpenRequested-=Open; }
        private void Open(string id) { if(manager!=null) manager.Open(id); }
    }
}
