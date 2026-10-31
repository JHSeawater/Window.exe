using UnityEngine;
namespace WindowExe.Desktop.Desktop.Window
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class SingleResizeHandle : MonoBehaviour
    {
        [SerializeField] private ResizeDirection direction;
        public ResizeDirection Direction => direction;
        public BoxCollider2D Collider => GetComponent<BoxCollider2D>();
        public void Init(Window window, WindowResizeHandle controller, ResizeDirection value) => direction=value;
    }
}
