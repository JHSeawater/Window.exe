using UnityEngine;

namespace WindowExe.Desktop.Desktop.Window
{
    public readonly struct WindowGeometryState
    {
        internal Window Owner { get; }
        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public Vector2 NormalPosition { get; }
        public Vector2 NormalSize { get; }
        public bool HasNormalGeometry { get; }

        internal WindowGeometryState(Window owner, Vector2 position, Vector2 size,
            Vector2 normalPosition, Vector2 normalSize, bool initialized)
        {
            Owner=owner; Position=position; Size=size;
            NormalPosition=normalPosition; NormalSize=normalSize; HasNormalGeometry=initialized;
        }
    }
}
