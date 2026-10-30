using UnityEngine;

namespace WindowExe.Desktop
{
    // Optional development receiver. Replace with the teammate's window-system listener.
    public sealed class DesktopOpenRequestProbe : MonoBehaviour
    {
        [SerializeField] private DesktopController desktop;
        public string LastItemId { get; private set; }
        public int RequestCount { get; private set; }
        public void Configure(DesktopController owner) => desktop = owner;
        private void OnEnable()
        {
            if (desktop != null) desktop.OpenRequested += Record;
        }
        private void OnDisable()
        {
            if (desktop != null) desktop.OpenRequested -= Record;
        }
        public void Record(string itemId)
        {
            LastItemId = itemId;
            RequestCount++;
            Debug.Log("Desktop open requested: " + itemId, this);
        }
    }
}
