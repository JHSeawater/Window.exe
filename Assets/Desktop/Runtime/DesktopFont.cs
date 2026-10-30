using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class DesktopFont : MonoBehaviour
    {
        private Font font;
        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            if (font == null) return;
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
        }
        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
