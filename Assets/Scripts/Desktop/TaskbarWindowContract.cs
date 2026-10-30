using System;
using System.Collections.Generic;
using UnityEngine;

namespace WindowExe.Desktop
{
    public enum TaskbarWindowAction { Activate, RestoreAndActivate, Minimize }

    // Immutable snapshots. Adapters deliver Changed and service requests on Unity's main thread.
    public sealed class TaskbarWindowInfo
    {
        public string WindowId { get; }
        public string ItemId { get; }
        public string OwnerWindowId { get; }
        public string Title { get; }
        public Sprite Icon { get; }
        public bool ShowInTaskbar { get; }
        public bool CanMinimize { get; }
        public bool IsMinimized { get; }
        public bool IsMaximized { get; }
        public bool IsModal { get; }
        public long CreationOrder { get; }

        public TaskbarWindowInfo(string windowId, string itemId, string title, Sprite icon = null,
            bool showInTaskbar = true, bool canMinimize = true, bool isMinimized = false,
            bool isMaximized = false, long creationOrder = 0, string ownerWindowId = null, bool isModal = false)
        {
            WindowId = windowId;
            ItemId = itemId;
            Title = title ?? "";
            Icon = icon;
            ShowInTaskbar = showInTaskbar;
            CanMinimize = canMinimize;
            IsMinimized = isMinimized;
            IsMaximized = isMaximized;
            CreationOrder = creationOrder;
            OwnerWindowId = ownerWindowId;
            IsModal = isModal;
        }

        public TaskbarWindowInfo With(bool? minimized = null, bool? maximized = null,
            string title = null, Sprite icon = null, bool? show = null, bool? canMinimize = null) =>
            new TaskbarWindowInfo(WindowId, ItemId, title ?? Title, icon, show ?? ShowInTaskbar,
                canMinimize ?? CanMinimize, minimized ?? IsMinimized, maximized ?? IsMaximized,
                CreationOrder, OwnerWindowId, IsModal);
    }

    public sealed class TaskbarWindowSnapshot
    {
        public IReadOnlyList<TaskbarWindowInfo> Windows { get; }
        public string ActiveWindowId { get; }
        public bool IsLocked { get; }
        public TaskbarWindowSnapshot(IEnumerable<TaskbarWindowInfo> windows, string activeWindowId, bool isLocked = false)
        {
            Windows = new List<TaskbarWindowInfo>(windows).AsReadOnly();
            ActiveWindowId = activeWindowId;
            IsLocked = isLocked;
        }
    }

    public interface ITaskbarWindowSource
    {
        event Action Changed;
        TaskbarWindowSnapshot GetSnapshot();
        bool Request(string windowId, TaskbarWindowAction action);
    }
}
