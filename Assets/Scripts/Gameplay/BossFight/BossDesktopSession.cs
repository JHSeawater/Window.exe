using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WindowExe.Desktop;
using WindowExe.Desktop.Desktop.Window;

namespace WindowExe.BossFight
{
    public enum BossOutcome { None, Success, BadEnding }
    public enum BossCleanupCause { EntryFailed, EndingFinished, SceneUnloading, ExternalRemoved }

    /// <summary>Desktop ownership and rollback. It owns neither combat simulation nor ending scenes.</summary>
    public sealed class BossDesktopSession : IWindowSessionPolicy, IDesktopInputSession, IWindowGeometryConstraint, ITaskbarWindowSource
    {
        private sealed class SavedWindow { public Window window; public string instance; public WindowState state; }
        private readonly WindowManager manager;
        private readonly DesktopInputRouter router;
        private readonly DesktopWindowSpace coordinates;
        private readonly DesktopController desktop;
        private readonly TaskbarController taskbar;
        private readonly object authority = new object();
        private readonly List<SavedWindow> savedWindows = new List<SavedWindow>();
        private readonly Dictionary<GameObject,bool> savedIcons = new Dictionary<GameObject,bool>();
        private ITaskbarWindowSource previousSource;
        private string previousActive;
        private bool previousDesktopEnabled, preparing, cleaning, bound, endingReady;
        private readonly HashSet<string> endingIds = new HashSet<string>();
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public BossOutcome Outcome { get; private set; }
        public bool IsClosed { get; private set; }
        public BossFightWindow View { get; private set; }
        public BossFightSpace Space { get; private set; }
        public BossRect Body { get; private set; }
        public bool DeferAreaConstraint => true;
        public event Action Changed;
        public event Action<DesktopCancelReason> Cancelled;
        public event Action<DesktopSpaceSnapshot> SpaceChanging;
        public event Action<BossCleanupCause> Removed;
        public event Action<TaskbarWindowAction> TaskbarRequested;
        public BossDesktopSession(WindowManager manager, DesktopInputRouter router, DesktopWindowSpace coordinates, DesktopController desktop, TaskbarController taskbar)
        { this.manager=manager; this.router=router; this.coordinates=coordinates; this.desktop=desktop; this.taskbar=taskbar; }

        public bool TryEnter(BossFightWindow prefab, BossRect initialBody, out string error)
        {
            error=null;
            if(bound || IsClosed || prefab==null || manager==null || router==null || coordinates==null || desktop==null || taskbar==null)
            { error="Missing desktop dependency or session already used."; return false; }
            Space=new BossFightSpace(coordinates.Snapshot);
            if(!Space.Fits(initialBody) || manager.IsLocked || manager.HasModal || manager.SessionPolicy!=null || router.HasSession || manager.GetProcess(BossFightWindow.ContentId)!=null)
            { error="Desktop cannot enter the fight in its current state."; return false; }
            foreach(var p in manager.ActiveProcesses)
                if(p.Instance==null || p.State!=WindowState.Minimized && !p.Instance.CanMinimize)
                { error="A window must be dismissed before entry."; return false; }
            var registration=manager.GetRegisteredPrefab(BossFightWindow.ContentId);
            if(registration!=null && registration!=prefab) { error="Boss content ID belongs to another prefab."; return false; }
            previousSource=taskbar.Source; previousActive=manager.ActiveWindowId; previousDesktopEnabled=desktop.enabled;
            preparing=true;
            try
            {
                if(!manager.TryInstallSessionPolicy(authority,this)) throw new InvalidOperationException("Request policy unavailable.");
                if(!router.TryBeginSession(authority,this)) throw new InvalidOperationException("Input ownership unavailable.");
                desktop.CancelInteraction();
                foreach(var key in desktop.GetComponentsInChildren<KeyFileMechanic>(true)) key.CancelPendingInteraction();
                EventSystem.current?.SetSelectedGameObject(null);
                foreach(var p in manager.ActiveProcesses) savedWindows.Add(new SavedWindow { window=p.Instance,instance=p.InstanceId,state=p.State });
                foreach(var icon in desktop.Icons)
                {
                    if(icon==null) continue; savedIcons[icon.gameObject]=icon.gameObject.activeSelf; icon.gameObject.SetActive(false);
                }
                desktop.enabled=false;
                foreach(var saved in savedWindows)
                    if(saved.state!=WindowState.Minimized && !manager.TryRequest(saved.window,WindowAction.Minimize,authority)) throw new InvalidOperationException("Window minimization refused.");
                if(registration==null) manager.RegisterPrefab(BossFightWindow.ContentId,prefab);
                View=manager.Open(BossFightWindow.ContentId,authority) as BossFightWindow;
                if(View==null || !manager.IsCurrent(View)) throw new InvalidOperationException("Boss window creation failed.");
                View.GeometryConstraint=this; View.Bind(Space.Scale);
                Body=initialBody; Project();
                taskbar.Bind(this);
                manager.Changed+=OnManagerChanged; coordinates.Changing+=OnSpaceChanging;
                bound=true; preparing=false; Changed?.Invoke(); return true;
            }
            catch(Exception e)
            {
                // An Open observer may fail after registration, before Open returns the instance.
                if(View==null) View=manager.GetProcess(BossFightWindow.ContentId)?.Instance as BossFightWindow;
                error=e.Message; Exit(BossCleanupCause.EntryFailed); return false;
            }
        }
        public bool AllowOpen(string id, object token) => !IsClosed &&
            (ReferenceEquals(authority,token) && (preparing || cleaning) || endingReady && endingIds.Contains(id));
        public bool AllowRequest(Window window, WindowAction action, object token)
        {
            if(IsClosed) return false;
            if(endingReady && endingIds.Contains(window.WindowId)) return true;
            if(!ReferenceEquals(authority,token)) return false;
            if(preparing || cleaning) return true;
            return Outcome==BossOutcome.None && ReferenceEquals(window,View) && action!=WindowAction.Close && action!=WindowAction.Maximize;
        }
        public bool KeepPositionOnMinimize(Window window) => ReferenceEquals(window,View);
        public bool Request(WindowAction action)
        { return View!=null && manager!=null && manager.TryRequest(View,action,authority); }
        public void Cancel(DesktopCancelReason reason) => Cancelled?.Invoke(reason);
        public bool TryConstrain(Window window, ref Vector2 position, ref Vector2 size)
        {
            if(!ReferenceEquals(window,View) || Space==null || !Space.Valid) return false;
            var world=Space.ToWorld(Body.Center);
            position=window.transform.parent!=null ? (Vector2)window.transform.parent.InverseTransformPoint(new Vector3(world.x,world.y,window.transform.position.z)) : world;
            size=new Vector2((float)Body.width,(float)Body.height)*Space.Scale; return true;
        }
        public bool SetBody(BossRect body)
        { if(IsClosed || Space==null || !Space.Fits(body)) return false; Body=body; Project(); return true; }
        public bool CommitSpace(DesktopSpaceSnapshot snapshot)
        {
            if(IsClosed) return false;
            Space=new BossFightSpace(snapshot);
            if(!Space.Fits(Body)) return false;
            View.Bind(Space.Scale); Project(); return true;
        }
        private void Project() { if(View!=null && Space!=null && Space.Valid) View.ApplyGeometry(Vector2.zero,Vector2.one); }
        private void OnSpaceChanging(DesktopSpaceSnapshot snapshot) => SpaceChanging?.Invoke(snapshot);
        private void OnManagerChanged()
        {
            if(IsClosed || cleaning) return;
            if(bound && (View==null || !manager.IsCurrent(View))) { Exit(BossCleanupCause.ExternalRemoved); return; }
            Changed?.Invoke();
        }
        public bool SetOutcome(BossOutcome outcome)
        { if(IsClosed || Outcome!=BossOutcome.None || outcome==BossOutcome.None) return false; Outcome=outcome; Changed?.Invoke(); return true; }
        public bool AllowEnding(string sessionId, IEnumerable<string> contentIds)
        {
            if(IsClosed || sessionId!=SessionId || Outcome==BossOutcome.None) return false;
            endingIds.Clear(); if(contentIds!=null) foreach(var id in contentIds)
                if(!string.IsNullOrEmpty(id) && id!=BossFightWindow.ContentId) endingIds.Add(id);
            endingReady=true;
            // Ending windows use the ordinary router, while the request policy still filters content.
            router.EndSession(authority); Changed?.Invoke(); return true;
        }
        public Window OpenEndingWindow(string sessionId, string contentId)
        { return sessionId==SessionId && endingReady && endingIds.Contains(contentId) ? manager.Open(contentId,authority) : null; }
        public bool Exit(BossCleanupCause cause, bool endingResourcesReleased=false)
        {
            if(IsClosed) return true;
            if(cause==BossCleanupCause.EntryFailed && !preparing) return false;
            if(cause==BossCleanupCause.EndingFinished && (Outcome==BossOutcome.None || !endingResourcesReleased)) return false;
            cleaning=true;
            if(manager!=null) manager.Changed-=OnManagerChanged;
            if(coordinates!=null) coordinates.Changing-=OnSpaceChanging;
            if(View!=null && manager!=null && manager.IsCurrent(View)) manager.TryRequest(View,WindowAction.Close,authority);
            if(taskbar!=null && ReferenceEquals(taskbar.Source,this))
                taskbar.Bind(cause==BossCleanupCause.EntryFailed ? previousSource : null);
            if(cause==BossCleanupCause.EntryFailed)
            {
                foreach(var item in savedIcons) if(item.Key!=null) item.Key.SetActive(item.Value);
                if(desktop!=null) desktop.enabled=previousDesktopEnabled;
                if(manager!=null)
                {
                    foreach(var saved in savedWindows)
                        if(saved.window!=null && manager.FindInstance(saved.instance)?.Instance==saved.window && saved.state!=WindowState.Minimized)
                            manager.TryRequest(saved.window,WindowAction.Restore,authority);
                    var active=manager.FindInstance(previousActive); if(active!=null) manager.TryRequest(active.Instance,WindowAction.Activate,authority);
                }
            }
            if(router!=null) router.EndSession(authority);
            if(manager!=null) manager.RemoveSessionPolicy(authority);
            IsClosed=true; cleaning=false; Removed?.Invoke(cause); Changed=null; Cancelled=null; SpaceChanging=null; TaskbarRequested=null; Removed=null;
            return true;
        }
        internal void AbortEntryPreparation()
        {
            if(IsClosed || Outcome!=BossOutcome.None) return;
            preparing=true; Exit(BossCleanupCause.EntryFailed);
        }
        public TaskbarWindowSnapshot GetSnapshot()
        {
            var list=new List<TaskbarWindowInfo>();
            var process=View!=null&&manager!=null?manager.GetProcess(View.WindowId):null;
            if(process!=null && process.Instance==View) list.Add(new TaskbarWindowInfo(process.InstanceId,process.Id,View.WindowTitle,View.TaskbarIcon,
                true,true,process.State==WindowState.Minimized,false,process.CreationOrder));
            if(endingReady && manager!=null) foreach(var item in manager.ActiveProcesses)
                if(item.Instance!=null && endingIds.Contains(item.Id)) list.Add(new TaskbarWindowInfo(item.InstanceId,item.Id,
                    item.Title,item.Instance.TaskbarIcon,item.Instance.ShowInTaskbar,item.Instance.CanMinimize,
                    item.State==WindowState.Minimized,item.State==WindowState.Maximized,item.CreationOrder,
                    item.Instance.OwnerWindowId,item.Instance.IsModal));
            return new TaskbarWindowSnapshot(list,manager!=null?manager.ActiveWindowId:null,IsClosed || Outcome!=BossOutcome.None&&!endingReady);
        }
        public bool Request(string windowId, TaskbarWindowAction action)
        {
            if(IsClosed || manager==null) return false;
            var process=manager.FindInstance(windowId);
            if(process==null) return false;
            if(endingReady && endingIds.Contains(process.Id)) return manager.TryRequest(process.Instance,
                action==TaskbarWindowAction.Minimize ? WindowAction.Minimize : WindowAction.Restore);
            if(Outcome!=BossOutcome.None || View==null || process.Instance!=View) return false;
            TaskbarRequested?.Invoke(action); return true;
        }
    }
}
