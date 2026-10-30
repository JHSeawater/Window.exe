using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class DesktopFont : MonoBehaviour
    {
        private Font font;
        private void Awake()
        {
            foreach (var label in GetComponentsInChildren<Text>(true)) Apply(label);
        }
        public void Apply(Text label)
        {
            if (label == null) return;
            if (font == null) font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            if (font != null) label.font = font;
        }
        private void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
