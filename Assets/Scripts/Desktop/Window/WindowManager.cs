using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    public enum WindowState { Normal, Minimized, Maximized }
    public enum WindowAction { Activate, Restore, Minimize, Maximize, Close }

    public sealed class WindowProcess
    {
        public string Id { get; }
        public string InstanceId { get; } = Guid.NewGuid().ToString("N");
        public string Title => Instance != null ? Instance.WindowTitle : Id;
        public Window Instance { get; }
        public long CreationOrder { get; }
        public WindowState State { get; internal set; }
        internal WindowProcess(string id, Window instance, long order)
        { Id = id; Instance = instance; CreationOrder = order; }
    }
    [Serializable] public struct WindowPrefabEntry { public string id; public Window prefab; }

    [DisallowMultipleComponent]
    public class WindowManager : MonoBehaviour
    {
        public static WindowManager Instance { get; private set; }
        [SerializeField] private List<WindowPrefabEntry> registeredPrefabs = new List<WindowPrefabEntry>();
        [SerializeField] private Transform windowRoot;
        private readonly Dictionary<string, WindowProcess> processes = new Dictionary<string, WindowProcess>();
        private readonly List<WindowProcess> stack = new List<WindowProcess>();
        private long sequence;
        private bool publishing, pending;
        private IWindowSessionPolicy sessionPolicy;
        private object policyOwner;
        public IWindowSessionPolicy SessionPolicy => sessionPolicy;
        public bool TryInstallSessionPolicy(object owner, IWindowSessionPolicy policy)
        {
            if (owner == null || policy == null || sessionPolicy != null) return false;
            policyOwner = owner; sessionPolicy = policy; Notify(); return true;
        }
        public bool RemoveSessionPolicy(object owner)
        {
            if (!ReferenceEquals(owner, policyOwner)) return false;
            sessionPolicy = null; policyOwner = null; Notify(); return true;
        }
        public string ActiveWindowId { get; private set; }
        public bool IsLocked { get; private set; }
        public bool HasModal => Modal()!=null;
        public event Action Changed;
        public event Action<WindowProcess> OnProcessOpened;
        public event Action<string> OnProcessClosed;
        public event Action<WindowProcess> OnProcessStateChanged;
        public event Action<WindowProcess> OnProcessFocused;
        public IReadOnlyList<WindowProcess> ActiveProcesses => new List<WindowProcess>(processes.Values).AsReadOnly();
        public IReadOnlyList<WindowProcess> DisplayOrder => stack.AsReadOnly();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebindSession()
        {
            if(Instance!=null) return;
            foreach(var manager in UnityEngine.Object.FindObjectsByType<WindowManager>())
                if(manager.isActiveAndEnabled) { Instance=manager; manager.ResetWindows(); break; }
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            if (windowRoot == null) windowRoot = transform;
        }
        private void OnDestroy() { processes.Clear(); stack.Clear(); ActiveWindowId=null; Changed?.Invoke(); if (Instance == this) Instance = null; }
        public void Configure(Transform root, IEnumerable<WindowPrefabEntry> entries)
        { windowRoot = root; registeredPrefabs = new List<WindowPrefabEntry>(entries); }
        public void RegisterPrefab(string id, Window prefab) { registeredPrefabs.RemoveAll(x=>x.id==id); registeredPrefabs.Add(new WindowPrefabEntry{id=id,prefab=prefab}); }
        public Window GetRegisteredPrefab(string id)
        { foreach(var entry in registeredPrefabs) if(entry.id==id) return entry.prefab; return null; }
        public bool CanRequest(Window expected, WindowAction action, object authority = null)
        {
            if(IsLocked||!IsCurrent(expected)) return false;
            if(sessionPolicy != null && !sessionPolicy.AllowRequest(expected, action, authority)) return false;
            var p=GetProcess(expected.WindowId); var modal=Modal();
            if(modal!=null&&modal!=p && !((action==WindowAction.Activate||action==WindowAction.Restore)&&IsOwner(p,modal))) return false;
            if(modal!=null&&modal!=p&&sessionPolicy!=null&&!sessionPolicy.AllowRequest(modal.Instance,action,authority)) return false;
            return action!=WindowAction.Minimize||expected.CanMinimize;
        }
        public WindowProcess GetProcess(string id) => id != null && processes.TryGetValue(id, out var p) ? p : null;
        public WindowProcess FindInstance(string id)
        { foreach (var p in processes.Values) if (p.InstanceId == id) return p; return null; }
        public bool IsWindowOpen(string id) => GetProcess(id) != null;
        public bool IsCurrent(Window w) => w != null && GetProcess(w.WindowId)?.Instance == w;
        public Window Open(string id, object authority = null)
        {
            if (IsLocked || string.IsNullOrEmpty(id)) return null;
            if (sessionPolicy != null && !sessionPolicy.AllowOpen(id, authority)) return null;
            var existing = GetProcess(id);
            if (existing != null) { var restored = TryRequest(existing.Instance, WindowAction.Restore, authority); return sessionPolicy == null || restored ? existing.Instance : null; }
            if (Modal() != null) return null;
            Window prefab = null;
            foreach (var entry in registeredPrefabs) if (entry.id == id) { prefab = entry.prefab; break; }
            if (prefab == null) { Debug.LogWarning("Unregistered desktop content: " + id, this); return null; }
            var w = Instantiate(prefab, windowRoot != null ? windowRoot : transform);
            w.gameObject.SetActive(false);
            w.InitializeVisuals();
            w.Attach(this, id);
            w.name = "Window_" + id;
            var p = new WindowProcess(id, w, ++sequence);
            processes.Add(id, p);
            stack.Add(p);
            w.gameObject.SetActive(true);
            if (DesktopWindowSpace.Instance != null) w.ConstrainTo(DesktopWindowSpace.Instance.Bounds);
            Focus(p);
            // Commit all state before notifying observers; callbacks may close/reopen this item.
            OnProcessOpened?.Invoke(p);
            if (IsCurrent(w) && ActiveWindowId == p.InstanceId) OnProcessFocused?.Invoke(p);
            Notify();
            return IsCurrent(w) ? w : null;
        }
        public void Minimize(string id) => TryRequest(GetProcess(id)?.Instance, WindowAction.Minimize);
        public void Enshow(string id) => TryRequest(GetProcess(id)?.Instance, WindowAction.Restore);
        public void Close(string id) => TryRequest(GetProcess(id)?.Instance, WindowAction.Close);
        public void ToggleMaximize(string id) => TryRequest(GetProcess(id)?.Instance, WindowAction.Maximize);
        public void FocusWindow(string id) => TryRequest(GetProcess(id)?.Instance, WindowAction.Activate);
        public bool TryRequest(Window expected, WindowAction action, object authority = null)
        {
            if (IsLocked || !IsCurrent(expected)) return false;
            if (sessionPolicy != null && !sessionPolicy.AllowRequest(expected, action, authority)) return false;
            var p = GetProcess(expected.WindowId);
            var modal = Modal();
            if (modal != null && modal != p)
            {
                if ((action == WindowAction.Activate || action == WindowAction.Restore) && IsOwner(p, modal)) p = modal;
                else return false;
            }
            var w = p.Instance;
            if (sessionPolicy != null && !sessionPolicy.AllowRequest(w, action, authority)) return false;
            if (action == WindowAction.Minimize && !w.CanMinimize) return false;
            if (action == WindowAction.Close)
            {
                Remove(p);
                OnProcessClosed?.Invoke(p.Id);
                Notify();
                return true;
            }
            if (action == WindowAction.Minimize)
            {
                if (sessionPolicy != null && sessionPolicy.KeepPositionOnMinimize(w)) DesktopInputRouter.Instance?.FinishFor(w);
                else DesktopInputRouter.Instance?.CancelFor(w);
                p.State = WindowState.Minimized;
                w.gameObject.SetActive(false);
                ChooseActive();
            }
            else
            {
                if (action == WindowAction.Maximize && p.State == WindowState.Minimized) return false;
                if (action == WindowAction.Maximize) { DesktopInputRouter.Instance?.CancelFor(w); w.ToggleMaximize(); }
                w.gameObject.SetActive(true);
                p.State = w.IsMaximized ? WindowState.Maximized : WindowState.Normal;
                Focus(p);
            }
            if (IsCurrent(w)) OnProcessStateChanged?.Invoke(p);
            if (IsCurrent(w) && ActiveWindowId == p.InstanceId) OnProcessFocused?.Invoke(p);
            Notify();
            return true;
        }
        public void SetLocked(bool value)
        {
            if (value == IsLocked) return;
            if (value) DesktopInputRouter.Instance?.Cancel();
            IsLocked = value;
            Notify();
        }
        public void SetPolicy(Window w, bool show, bool minimize, bool modal = false, string owner = null)
        {
            if (w == null) return;
            w.SetPolicyFields(show, minimize, modal, owner);
            if (IsCurrent(w)) { if (modal && w.gameObject.activeSelf) Focus(GetProcess(w.WindowId)); Notify(); }
        }
        public void AppearanceChanged(Window w) { if (IsCurrent(w)) Notify(); }
        private WindowProcess Modal()
        { for (var i = stack.Count - 1; i >= 0; i--) if (Visible(stack[i]) && stack[i].Instance.IsModal) return stack[i]; return null; }
        private bool IsOwner(WindowProcess owner, WindowProcess child)
        {
            var visited = new HashSet<string>();
            while (child != null && visited.Add(child.InstanceId))
            {
                if (child.Instance.OwnerWindowId == owner.InstanceId) return true;
                child = FindInstance(child.Instance.OwnerWindowId);
            }
            return false;
        }
        private static bool Visible(WindowProcess p) => p.Instance != null && p.State != WindowState.Minimized && p.Instance.gameObject.activeSelf;
        private void Focus(WindowProcess p)
        { stack.Remove(p); stack.Add(p); ActiveWindowId = p.InstanceId; ApplySorting(); }
        private void ChooseActive()
        {
            ActiveWindowId = null;
            for (var i = stack.Count - 1; i >= 0; i--) if (Visible(stack[i])) { ActiveWindowId = stack[i].InstanceId; break; }
            ApplySorting();
        }
        public void ApplySorting()
        { for (var i = 0; i < stack.Count; i++) if (stack[i].Instance != null) stack[i].Instance.ApplySorting(i * 32); }
        private void Remove(WindowProcess p)
        {
            var children = new List<WindowProcess>();
            foreach (var candidate in processes.Values) if (IsOwner(p, candidate)) children.Add(candidate);
            foreach (var child in children) if (GetProcess(child.Id) == child) Remove(child);
            DesktopInputRouter.Instance?.CancelFor(p.Instance);
            processes.Remove(p.Id); stack.Remove(p);
            if (p.Instance != null) { p.Instance.gameObject.SetActive(false); if (Application.isPlaying) Destroy(p.Instance.gameObject); else DestroyImmediate(p.Instance.gameObject); }
            ChooseActive();
        }
        public void RemovedExternally(Window w)
        {
            // Reference identity is essential when old OnDestroy follows a same-frame reopen.
            var p = GetProcess(w.WindowId);
            if (p == null || !ReferenceEquals(p.Instance, w)) return;
            Remove(p); OnProcessClosed?.Invoke(p.Id); Notify();
        }
        public void ResetWindows()
        {
            DesktopInputRouter.Instance?.Cancel();
            foreach (var p in new List<WindowProcess>(processes.Values)) if (GetProcess(p.Id) == p) Remove(p);
            ActiveWindowId = null; IsLocked = false; Notify();
        }
        public void ConstrainAll(Rect bounds)
        { foreach (var p in processes.Values) if (p.Instance != null) p.Instance.ConstrainTo(bounds); }
        private void Notify()
        {
            pending = true;
            if (publishing) return;
            publishing = true;
            try { while (pending) { pending = false; Changed?.Invoke(); } }
            finally { publishing = false; }
        }
    }
}
