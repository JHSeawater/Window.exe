using UnityEngine;

namespace WindowExe.Desktop
{
    [CreateAssetMenu(menuName = "WINDOW.exe/Desktop Item")]
    public sealed class DesktopItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite corruptedIcon;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Sprite CorruptedIcon => corruptedIcon;

        public void SetCorruptedIcon(Sprite sprite) => corruptedIcon = sprite;

        public void Configure(string id, string title, Sprite sprite)
        {
            itemId = id;
            displayName = title;
            icon = sprite;
        }
    }
}
