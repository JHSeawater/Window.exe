using UnityEngine;
using UnityEngine.UI;

namespace WindowExe.Desktop
{
    public sealed class DesktopFont : MonoBehaviour
    {
        private void Awake()
        {
            GameTypography.ApplyHierarchy(gameObject);
        }
        public void Apply(Text label)
        {
            GameTypography.Apply(label);
        }
    }
}
