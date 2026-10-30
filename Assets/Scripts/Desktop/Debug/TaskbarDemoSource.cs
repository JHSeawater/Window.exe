using System;
using UnityEngine;

namespace WindowExe.Desktop
{
    public sealed class TaskbarDemoSource : MonoBehaviour, ITaskbarWindowSource
    {
        [SerializeField] private DesktopController desktop;
        private readonly TaskbarDemoModel model = new TaskbarDemoModel();
        public TaskbarDemoModel Model => model;
        public event Action Changed { add => model.Changed += value; remove => model.Changed -= value; }
        public TaskbarWindowSnapshot GetSnapshot() => model.GetSnapshot();
        public bool Request(string id, TaskbarWindowAction action) => model.Request(id, action);

        public void Configure(DesktopController owner)
        {
            if (desktop != null) desktop.OpenRequested -= OnOpen;
            desktop = owner;
            if (isActiveAndEnabled && desktop != null) desktop.OpenRequested += OnOpen;
        }

        private void OnEnable() { if (desktop != null) desktop.OpenRequested += OnOpen; }
        private void OnDisable() { if (desktop != null) desktop.OpenRequested -= OnOpen; }
        private void OnOpen(string itemId)
        {
            foreach (var icon in desktop.Icons)
                if (icon != null && icon.Item.ItemId == itemId)
                {
                    model.Open(itemId, icon.Item.DisplayName, icon.Item.Icon);
                    return;
                }
        }

        [ContextMenu("Close active simulated window")]
        public void CloseActive() => model.Close(model.GetSnapshot().ActiveWindowId);
        [ContextMenu("Reset simulated window state")]
        public void ResetState() => model.Reset();
    }
}
