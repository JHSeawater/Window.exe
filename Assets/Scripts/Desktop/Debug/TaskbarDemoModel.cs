using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.Desktop
{
    // State-only development provider; it does not implement real windows or their geometry.
    public sealed class TaskbarDemoModel : ITaskbarWindowSource
    {
        private readonly List<TaskbarWindowInfo> windows = new List<TaskbarWindowInfo>();
        private readonly List<string> activationHistory = new List<string>();
        private string active;
        private bool locked;
        private long nextOrder;
        public event Action Changed;
        public TaskbarWindowSnapshot GetSnapshot() => new TaskbarWindowSnapshot(windows, active, locked);
        public TaskbarWindowInfo Find(string id) => windows.Find(w => w.WindowId == id);

        public string Open(string itemId, string title, Sprite icon = null, bool show = true,
            bool canMinimize = true, string ownerId = null, bool modal = false)
        {
            if (locked) return null;
            var existing = string.IsNullOrEmpty(itemId) ? null : windows.Find(w => w.ItemId == itemId);
            if (existing != null)
            {
                Request(existing.WindowId, TaskbarWindowAction.RestoreAndActivate);
                return existing.WindowId;
            }
            if (CurrentModal() != null || (ownerId != null && Find(ownerId) == null) || (modal && ownerId == null)) return null;
            var id = Guid.NewGuid().ToString("N");
            windows.Add(new TaskbarWindowInfo(id, itemId, title, icon, show, canMinimize,
                creationOrder: nextOrder++, ownerWindowId: ownerId, isModal: modal));
            Activate(id);
            Changed?.Invoke();
            return id;
        }

        public bool Request(string windowId, TaskbarWindowAction action)
        {
            var target = Find(windowId);
            if (locked || target == null) return false;
            var modal = CurrentModal();
            if (modal != null && modal.WindowId != windowId)
            {
                if (!IsAncestor(windowId, modal.WindowId) || action == TaskbarWindowAction.Minimize) return false;
                Activate(modal.WindowId);
                Changed?.Invoke();
                return true;
            }
            if (action == TaskbarWindowAction.Minimize)
            {
                if (!target.CanMinimize || target.IsModal) return false;
                Replace(target.With(minimized: true, icon: target.Icon));
                if (active == windowId) ChooseNextActive();
            }
            else
            {
                Replace(target.With(minimized: false, icon: target.Icon));
                Activate(windowId);
            }
            Changed?.Invoke();
            return true;
        }

        public bool Close(string windowId)
        {
            if (Find(windowId) == null) return false;
            var removed = new HashSet<string> { windowId };
            bool expanded;
            do
            {
                expanded = false;
                foreach (var window in windows)
                    if (window.OwnerWindowId != null && removed.Contains(window.OwnerWindowId))
                        expanded |= removed.Add(window.WindowId);
            } while (expanded);
            windows.RemoveAll(w => removed.Contains(w.WindowId));
            activationHistory.RemoveAll(removed.Contains);
            if (removed.Contains(active ?? "")) ChooseNextActive();
            Changed?.Invoke();
            return true;
        }

        public bool SetMaximized(string id, bool value)
        {
            var window = Find(id);
            if (window == null) return false;
            Replace(window.With(maximized: value, icon: window.Icon));
            Changed?.Invoke();
            return true;
        }

        public bool SetAppearance(string id, string title, Sprite icon, bool show, bool canMinimize)
        {
            var window = Find(id);
            if (window == null) return false;
            Replace(window.With(title: title, icon: icon, show: show, canMinimize: canMinimize));
            Changed?.Invoke();
            return true;
        }

        public void SetLocked(bool value) { locked = value; Changed?.Invoke(); }
        public void Reset()
        {
            windows.Clear();
            activationHistory.Clear();
            active = null;
            locked = false;
            nextOrder = 0;
            Changed?.Invoke();
        }

        private void Replace(TaskbarWindowInfo window) => windows[windows.FindIndex(w => w.WindowId == window.WindowId)] = window;
        private void Activate(string id)
        {
            active = id;
            activationHistory.Remove(id);
            activationHistory.Add(id);
        }

        private TaskbarWindowInfo CurrentModal() => windows.FindLast(w => w.IsModal && !w.IsMinimized);
        private bool IsAncestor(string ancestor, string child)
        {
            for (var current = Find(child); current != null && current.OwnerWindowId != null; current = Find(current.OwnerWindowId))
                if (current.OwnerWindowId == ancestor) return true;
            return false;
        }

        private void ChooseNextActive()
        {
            active = null;
            var modal = CurrentModal();
            if (modal != null) { Activate(modal.WindowId); return; }
            for (var i = activationHistory.Count - 1; i >= 0; i--)
            {
                var window = Find(activationHistory[i]);
                if (window != null && !window.IsMinimized) { active = window.WindowId; return; }
            }
        }
    }
}
