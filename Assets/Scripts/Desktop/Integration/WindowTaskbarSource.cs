using System;
using System.Collections.Generic;
using UnityEngine;
using WindowExe.Desktop.Desktop.Window;
namespace WindowExe.Desktop
{
    public sealed class WindowTaskbarSource : MonoBehaviour, ITaskbarWindowSource
    {
        [SerializeField] private WindowManager manager;
        private bool subscribed;
        public event Action Changed;
        public void Configure(WindowManager value) { Unsubscribe(); manager=value; Subscribe(); Changed?.Invoke(); }
        private void OnEnable() { Subscribe(); Changed?.Invoke(); }
        private void OnDisable() { Unsubscribe(); Changed?.Invoke(); }
        private void Subscribe() { if(!subscribed&&manager!=null&&isActiveAndEnabled) { manager.Changed+=Notify; subscribed=true; } }
        private void Unsubscribe() { if(subscribed&&manager!=null) manager.Changed-=Notify; subscribed=false; }
        private void Notify() => Changed?.Invoke();
        public TaskbarWindowSnapshot GetSnapshot()
        {
            var list=new List<TaskbarWindowInfo>();
            if(isActiveAndEnabled&&manager!=null) foreach(var p in manager.ActiveProcesses)
            {
                var w=p.Instance; if(w==null) continue;
                list.Add(new TaskbarWindowInfo(p.InstanceId,p.Id,w.WindowTitle,w.TaskbarIcon,w.ShowInTaskbar,w.CanMinimize,
                    p.State==WindowState.Minimized,w.IsMaximized,p.CreationOrder,w.OwnerWindowId,w.IsModal));
            }
            return new TaskbarWindowSnapshot(list,isActiveAndEnabled&&manager!=null?manager.ActiveWindowId:null,!isActiveAndEnabled||manager==null||manager.IsLocked);
        }
        public bool Request(string id,TaskbarWindowAction action)
        {
            if(!isActiveAndEnabled||manager==null) return false;
            var p=manager.FindInstance(id); if(p==null) return false;
            return manager.TryRequest(p.Instance,action==TaskbarWindowAction.Minimize?WindowAction.Minimize:action==TaskbarWindowAction.RestoreAndActivate?WindowAction.Restore:WindowAction.Activate);
        }
    }
}
