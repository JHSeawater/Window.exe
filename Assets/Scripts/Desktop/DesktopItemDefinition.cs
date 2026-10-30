using UnityEngine;

namespace WindowExe.Desktop
{
    [CreateAssetMenu(menuName = "WINDOW.exe/Desktop Item")]
    public sealed class DesktopItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;

        public void Configure(string id, string title, Sprite sprite)
        {
            itemId = id;
            displayName = title;
            icon = sprite;
        }
    }
}
