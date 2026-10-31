using TMPro;
using UnityEngine;

namespace WindowExe.Desktop
{
    [CreateAssetMenu(menuName = "WINDOW.exe/Game Font Settings")]
    public sealed class GameFontSettings : ScriptableObject
    {
        [SerializeField] private Font uiFont;
        [SerializeField] private TMP_FontAsset tmpFont;

        public Font UiFont => uiFont;
        public TMP_FontAsset TmpFont => tmpFont;

        public void Configure(Font source, TMP_FontAsset textMeshProFont)
        {
            uiFont = source;
            tmpFont = textMeshProFont;
        }
    }
}
